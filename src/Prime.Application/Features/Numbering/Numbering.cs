using System.Text.RegularExpressions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Prime.Application.Common;
using Prime.Application.Common.Interfaces;
using Prime.Domain.DomainServices;
using Prime.Domain.Entities.Forms;
using Prime.Domain.Enums;

namespace Prime.Application.Features.Numbering;

public sealed record CreateNumberingSchemeRequest(
    string LegalBasis, DateOnly EffectiveDate, string? Remarks,
    NumberedDocumentKind AppliesTo, string Name, string Pattern, string? ValidationRegex, bool AllowManualEntry);

public sealed record NumberingSchemeDto(
    Guid Id, NumberedDocumentKind AppliesTo, string Name, string Pattern, string? ValidationRegex, bool AllowManualEntry,
    string LegalBasis, DateOnly EffectiveDate, DateOnly? EndDate, WorkflowStatus Status,
    Guid? CreatedBy, DateTimeOffset CreatedAt, Guid? ApprovedBy, DateTimeOffset? ApprovedAt, string? Remarks,
    string Example);

public sealed class CreateNumberingSchemeRequestValidator : AbstractValidator<CreateNumberingSchemeRequest>
{
    public CreateNumberingSchemeRequestValidator()
    {
        RuleFor(x => x.LegalBasis).NotEmpty().MaximumLength(500);
        RuleFor(x => x.EffectiveDate).NotEqual(default(DateOnly)).WithMessage("effectiveDate is required.");
        RuleFor(x => x.Remarks).MaximumLength(1000);
        RuleFor(x => x.AppliesTo).IsInEnum();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Pattern).MaximumLength(200)
            .Must(p => NumberPattern.Validate(p) is null).WithMessage(x => $"pattern: {NumberPattern.Validate(x.Pattern)}.");
        RuleFor(x => x.ValidationRegex).MaximumLength(500)
            .Must(BeValidRegex).WithMessage("validationRegex is not a valid regular expression.");
    }

    private static bool BeValidRegex(string? pattern)
    {
        if (string.IsNullOrEmpty(pattern))
        {
            return true;
        }
        try
        {
            _ = new Regex(pattern, RegexOptions.None, TimeSpan.FromSeconds(1));
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}

public interface INumberingService
{
    Task<Result<NumberingSchemeDto>> CreateAsync(CreateNumberingSchemeRequest request, CancellationToken cancellationToken = default);
    Task<Result<NumberingSchemeDto>> ApproveAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<NumberingSchemeDto>> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<NumberingSchemeDto>>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// The number for a new document of <paramref name="kind"/> dated
    /// <paramref name="asOf"/>. With no scheme in force, <paramref name="manualValue"/>
    /// is required and used as typed. With a scheme, a typed value must be
    /// allowed and match its format; otherwise the next number is generated.
    /// Generation advances the sequence in the caller's transaction.
    /// </summary>
    Task<Result<string>> AssignAsync(NumberedDocumentKind kind, NumberContext context, string? manualValue, DateOnly asOf,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The generated number, or null when no scheme is in force (a scheme-less
    /// document simply has no number). For documents never numbered by hand, e.g. bills.
    /// </summary>
    Task<Result<string?>> GenerateIfConfiguredAsync(NumberedDocumentKind kind, NumberContext context, DateOnly asOf,
        CancellationToken cancellationToken = default);
}

/// <summary>docs/FORMS-REVISION-PLAN.md §4.4.</summary>
public sealed class NumberingService(
    IApplicationDbContext db,
    IValidator<CreateNumberingSchemeRequest> validator,
    ICurrentUserService currentUser,
    INumberSequenceAllocator allocator) : INumberingService
{
    private static readonly NumberContext ExampleContext = new(DateTime.UtcNow.Year, "PPPP", "MMMM", "BBBBBB");

    public async Task<Result<NumberingSchemeDto>> CreateAsync(CreateNumberingSchemeRequest request, CancellationToken cancellationToken = default)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure<NumberingSchemeDto>("VALIDATION_FAILED", string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)));
        }
        var scheme = new NumberingScheme
        {
            LegalBasis = request.LegalBasis, EffectiveDate = request.EffectiveDate, Remarks = request.Remarks,
            AppliesTo = request.AppliesTo, Name = request.Name, Pattern = request.Pattern,
            ValidationRegex = string.IsNullOrWhiteSpace(request.ValidationRegex) ? null : request.ValidationRegex,
            AllowManualEntry = request.AllowManualEntry,
        };
        db.NumberingSchemes.Add(scheme);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success(ToDto(scheme));
    }

    public async Task<Result<NumberingSchemeDto>> ApproveAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var scheme = await db.NumberingSchemes.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (scheme is null)
        {
            return NotFound();
        }
        if (await ConfigurationApproval.ApproveAsync(db, currentUser, db.NumberingSchemes.Where(x => x.AppliesTo == scheme.AppliesTo),
                scheme, "NUMBERING_SCHEME", cancellationToken) is { } failure)
        {
            return Result.Failure<NumberingSchemeDto>(failure.Code!, failure.Message!);
        }
        return Result.Success(ToDto(scheme));
    }

    public async Task<Result<NumberingSchemeDto>> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        await db.NumberingSchemes.FirstOrDefaultAsync(x => x.Id == id, cancellationToken) is { } scheme
            ? Result.Success(ToDto(scheme))
            : NotFound();

    public async Task<Result<IReadOnlyList<NumberingSchemeDto>>> ListAsync(CancellationToken cancellationToken = default) =>
        Result.Success<IReadOnlyList<NumberingSchemeDto>>((await db.NumberingSchemes
            .OrderBy(x => x.AppliesTo).ThenByDescending(x => x.EffectiveDate).ThenByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken)).Select(ToDto).ToList());

    public async Task<Result<string>> AssignAsync(NumberedDocumentKind kind, NumberContext context, string? manualValue, DateOnly asOf,
        CancellationToken cancellationToken = default)
    {
        var manual = string.IsNullOrWhiteSpace(manualValue) ? null : manualValue.Trim();
        var scheme = await db.NumberingSchemes.InForce(asOf).FirstOrDefaultAsync(x => x.AppliesTo == kind, cancellationToken);
        if (scheme is null)
        {
            return manual is null
                ? Result.Failure<string>("NUMBER_REQUIRED", $"No {kind} numbering scheme is in force; enter the number.")
                : Result.Success(manual);
        }
        if (manual is not null)
        {
            if (!scheme.AllowManualEntry)
            {
                return Result.Failure<string>("NUMBER_MANUAL_ENTRY_NOT_ALLOWED",
                    $"Numbers are generated by the '{scheme.Name}' scheme; leave the number empty.");
            }
            if (scheme.ValidationRegex is { } regex && !Regex.IsMatch(manual, regex, RegexOptions.None, TimeSpan.FromSeconds(1)))
            {
                return Result.Failure<string>("NUMBER_FORMAT_INVALID", $"'{manual}' does not match the '{scheme.Name}' format.");
            }
            return Result.Success(manual);
        }
        return await GenerateAsync(scheme, context, cancellationToken);
    }

    public async Task<Result<string?>> GenerateIfConfiguredAsync(NumberedDocumentKind kind, NumberContext context, DateOnly asOf,
        CancellationToken cancellationToken = default)
    {
        var scheme = await db.NumberingSchemes.InForce(asOf).FirstOrDefaultAsync(x => x.AppliesTo == kind, cancellationToken);
        if (scheme is null)
        {
            return Result.Success<string?>(null);
        }
        var generated = await GenerateAsync(scheme, context, cancellationToken);
        return generated.IsSuccess ? Result.Success<string?>(generated.Value) : Result.Failure<string?>(generated.Code!, generated.Message!);
    }

    private async Task<Result<string>> GenerateAsync(NumberingScheme scheme, NumberContext context, CancellationToken ct)
    {
        if (NumberPattern.MissingValues(scheme.Pattern, context) is { Count: > 0 } missing)
        {
            return Result.Failure<string>("NUMBER_CONTEXT_MISSING",
                $"The '{scheme.Name}' pattern needs {string.Join(", ", missing)}, which this record does not have.");
        }
        var sequence = await allocator.NextAsync(scheme.Id, NumberPattern.ScopeKey(scheme.Pattern, context), ct);
        return Result.Success(NumberPattern.Format(scheme.Pattern, context, sequence));
    }

    private static Result<NumberingSchemeDto> NotFound() =>
        Result.Failure<NumberingSchemeDto>("NUMBERING_SCHEME_NOT_FOUND", "No numbering scheme was found with the given id.");

    private static NumberingSchemeDto ToDto(NumberingScheme x) => new(
        x.Id, x.AppliesTo, x.Name, x.Pattern, x.ValidationRegex, x.AllowManualEntry, x.LegalBasis, x.EffectiveDate, x.EndDate,
        x.Status, x.CreatedBy, x.CreatedAt, x.ApprovedBy, x.ApprovedAt, x.Remarks,
        NumberPattern.Format(x.Pattern, ExampleContext, 1));
}

/// <summary>Builds a <see cref="NumberContext"/> from a location's reference codes.</summary>
public static class NumberContexts
{
    public static async Task<NumberContext> ForLocationAsync(IApplicationDbContext db, Guid provinceId, Guid municipalityId, Guid barangayId,
        int year, CancellationToken ct) =>
        new(year,
            await db.Provinces.Where(x => x.Id == provinceId).Select(x => x.PsgcCode).FirstOrDefaultAsync(ct),
            await db.Municipalities.Where(x => x.Id == municipalityId).Select(x => x.PsgcCode).FirstOrDefaultAsync(ct),
            await db.Barangays.Where(x => x.Id == barangayId).Select(x => x.PsgcCode).FirstOrDefaultAsync(ct));

    public static async Task<NumberContext> ForPropertyAsync(IApplicationDbContext db, Guid propertyId, int year, CancellationToken ct)
    {
        var p = await db.Properties.Where(x => x.Id == propertyId)
            .Select(x => new { x.ProvinceId, x.MunicipalityId, x.BarangayId }).SingleAsync(ct);
        return await ForLocationAsync(db, p.ProvinceId, p.MunicipalityId, p.BarangayId, year, ct);
    }
}
