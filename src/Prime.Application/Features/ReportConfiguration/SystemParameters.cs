using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Prime.Application.Common;
using Prime.Application.Common.Interfaces;
using Prime.Domain.Entities;
using Prime.Domain.Enums;

namespace Prime.Application.Features.ReportConfiguration;

/// <summary>A parameter PRIME reads: its code, what it is and its unit. The value is the office's (CLAUDE.md §5, §7).</summary>
public sealed record SystemParameterDefinition(string Code, string Name, string Description, string Unit);

/// <summary>The dated parameters PRIME reads (docs/analysis/reporting.md §10, Q16). None has a built-in value.</summary>
public static class SystemParameterCatalog
{
    public const string QrrpaResidentialBuildingThreshold = "QRRPA_RESIDENTIAL_BUILDING_THRESHOLD";

    public static readonly IReadOnlyList<SystemParameterDefinition> All =
    [
        new(QrrpaResidentialBuildingThreshold, "QRRPA residential building value threshold",
            "The market value at which the quarterly report splits the residential row's buildings into two columns (at or below, and over). "
            + "DOMAIN VERIFICATION REQUIRED: its legal basis.", "PHP"),
    ];

    public static SystemParameterDefinition? Find(string code) => All.FirstOrDefault(p => p.Code == code);
}

public sealed record CreateSystemParameterRequest(string Code, decimal Value, string LegalBasis, DateOnly EffectiveDate, string? Description, string? Remarks);

public sealed record SystemParameterDto(
    Guid Id, string Code, string Name, string Unit, decimal Value, string LegalBasis, DateOnly EffectiveDate, DateOnly? EndDate, WorkflowStatus Status,
    string? Description, string? Remarks, Guid? CreatedBy, Guid? ApprovedBy, DateTimeOffset? ApprovedAt);

public sealed class CreateSystemParameterRequestValidator : AbstractValidator<CreateSystemParameterRequest>
{
    public CreateSystemParameterRequestValidator()
    {
        RuleFor(x => x.Code).Must(c => SystemParameterCatalog.Find(c) is not null).WithMessage("code is not a parameter PRIME reads.");
        RuleFor(x => x.Value).GreaterThanOrEqualTo(0m).PrecisionScale(18, 6, true);
        RuleFor(x => x.LegalBasis).NotEmpty().MaximumLength(500);
        RuleFor(x => x.EffectiveDate).NotEqual(default(DateOnly)).WithMessage("effectiveDate is required.");
        RuleFor(x => x.Description).MaximumLength(1000);
        RuleFor(x => x.Remarks).MaximumLength(1000);
    }
}

public interface ISystemParameterService
{
    IReadOnlyList<SystemParameterDefinition> Catalog();
    Task<Result<SystemParameterDto>> CreateAsync(CreateSystemParameterRequest request, CancellationToken cancellationToken = default);
    Task<Result<SystemParameterDto>> ApproveAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<SystemParameterDto>>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>The approved value in force on <paramref name="on"/>, with its version; null if none is set.</summary>
    Task<SystemParameterDto?> InForceAsync(string code, DateOnly on, CancellationToken cancellationToken = default);
}

/// <summary>
/// Dated system parameters (docs/analysis/reporting.md §10, Q16): values an issuance or ordinance sets, entered with their
/// legal basis and approved by a second user; a new version takes over from its effective date. Province-wide.
/// </summary>
public sealed class SystemParameterService(
    IApplicationDbContext db,
    IValidator<CreateSystemParameterRequest> validator,
    ICurrentUserService currentUser,
    IJurisdiction jurisdiction) : ISystemParameterService
{
    public IReadOnlyList<SystemParameterDefinition> Catalog() => SystemParameterCatalog.All;

    public async Task<Result<SystemParameterDto>> CreateAsync(CreateSystemParameterRequest r, CancellationToken cancellationToken = default)
    {
        var validation = await validator.ValidateAsync(r, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure<SystemParameterDto>("VALIDATION_FAILED", string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)));
        }
        if (jurisdiction.Restricted)
        {
            return Result.Failure<SystemParameterDto>(JurisdictionErrors.Code, "A system parameter is set by an office whose jurisdiction is the whole province.");
        }
        var parameter = new SystemParameter
        {
            Code = r.Code, Value = r.Value, LegalBasis = r.LegalBasis.Trim(), EffectiveDate = r.EffectiveDate,
            Description = string.IsNullOrWhiteSpace(r.Description) ? null : r.Description.Trim(),
            Remarks = string.IsNullOrWhiteSpace(r.Remarks) ? null : r.Remarks.Trim(),
        };
        db.SystemParameters.Add(parameter);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success(ToDto(parameter));
    }

    public async Task<Result<SystemParameterDto>> ApproveAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var parameter = await db.SystemParameters.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (parameter is null)
        {
            return Result.Failure<SystemParameterDto>("SYSTEM_PARAMETER_NOT_FOUND", "No system parameter was found with the given id.");
        }
        if (jurisdiction.Restricted)
        {
            return Result.Failure<SystemParameterDto>(JurisdictionErrors.Code, "A system parameter is approved by an office whose jurisdiction is the whole province.");
        }
        if (await ConfigurationApproval.ApproveAsync(db, currentUser, db.SystemParameters.Where(x => x.Code == parameter.Code), parameter, "SYSTEM_PARAMETER",
                cancellationToken) is { } failure)
        {
            return Result.Failure<SystemParameterDto>(failure.Code!, failure.Message!);
        }
        return Result.Success(ToDto(parameter));
    }

    public async Task<Result<IReadOnlyList<SystemParameterDto>>> ListAsync(CancellationToken cancellationToken = default)
    {
        var rows = await db.SystemParameters.AsNoTracking().OrderBy(x => x.Code).ThenByDescending(x => x.EffectiveDate).ToListAsync(cancellationToken);
        return Result.Success<IReadOnlyList<SystemParameterDto>>(rows.Select(ToDto).ToList());
    }

    public async Task<SystemParameterDto?> InForceAsync(string code, DateOnly on, CancellationToken cancellationToken = default) =>
        await db.SystemParameters.AsNoTracking().InForce(on).Where(x => x.Code == code).OrderByDescending(x => x.EffectiveDate)
            .FirstOrDefaultAsync(cancellationToken) is { } p ? ToDto(p) : null;

    private static SystemParameterDto ToDto(SystemParameter x)
    {
        var definition = SystemParameterCatalog.Find(x.Code);
        return new(x.Id, x.Code, definition?.Name ?? x.Code, definition?.Unit ?? "", x.Value, x.LegalBasis, x.EffectiveDate, x.EndDate, x.Status,
            x.Description, x.Remarks, x.CreatedBy, x.ApprovedBy, x.ApprovedAt);
    }
}
