using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Prime.Application.Common;
using Prime.Application.Common.Interfaces;
using Prime.Domain.Entities;
using Prime.Domain.Enums;

namespace Prime.Application.Features.LevyRates;

/// <param name="MunicipalityId">Null: the whole province.</param>
/// <param name="ClassificationId">Null: every classification.</param>
public sealed record CreateLevyRateRequest(
    LevyKind Kind, Guid? MunicipalityId, Guid? ClassificationId, decimal RatePercent, string LegalBasis, DateOnly EffectiveDate,
    string? Description, string? Remarks);

public sealed record LevyRateDto(
    Guid Id, string Code, LevyKind Kind, Guid? MunicipalityId, string? MunicipalityName, Guid? ClassificationId, string? ClassificationName,
    decimal RatePercent, string LegalBasis, DateOnly EffectiveDate, DateOnly? EndDate, WorkflowStatus Status, string? Description, string? Remarks,
    Guid? CreatedBy, Guid? ApprovedBy, DateTimeOffset? ApprovedAt);

public sealed class CreateLevyRateRequestValidator : AbstractValidator<CreateLevyRateRequest>
{
    public CreateLevyRateRequestValidator()
    {
        RuleFor(x => x.Kind).IsInEnum();
        RuleFor(x => x.RatePercent).GreaterThan(0m).LessThanOrEqualTo(100m).PrecisionScale(9, 6, true);
        RuleFor(x => x.LegalBasis).NotEmpty().MaximumLength(500);
        RuleFor(x => x.EffectiveDate).NotEqual(default(DateOnly)).WithMessage("effectiveDate is required.");
        RuleFor(x => x.Description).MaximumLength(1000);
        RuleFor(x => x.Remarks).MaximumLength(1000);
    }
}

/// <summary>
/// The levy rates in force on one date, read once for a report: <see cref="Find"/> gives the most specific rate of a levy
/// for a municipality and classification.
/// </summary>
public sealed class LevyRateTable(IReadOnlyList<LevyRateDto> rates)
{
    public IReadOnlyList<LevyRateDto> Rates => rates;

    /// <summary>The municipality's rate for the classification, else its rate for every class, else the province's, likewise; null if none.</summary>
    public LevyRateDto? Find(LevyKind kind, Guid municipalityId, Guid? classificationId) =>
        Match(kind, municipalityId, classificationId) ?? Match(kind, municipalityId, null)
        ?? Match(kind, null, classificationId) ?? Match(kind, null, null);

    private LevyRateDto? Match(LevyKind kind, Guid? municipalityId, Guid? classificationId) =>
        classificationId is null && municipalityId is null
            ? rates.FirstOrDefault(r => r.Kind == kind && r.MunicipalityId is null && r.ClassificationId is null)
            : rates.FirstOrDefault(r => r.Kind == kind && r.MunicipalityId == municipalityId && r.ClassificationId == classificationId);
}

public interface ILevyRateService
{
    Task<Result<LevyRateDto>> CreateAsync(CreateLevyRateRequest request, CancellationToken cancellationToken = default);
    Task<Result<LevyRateDto>> ApproveAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<LevyRateDto>>> ListAsync(bool inForceOnly, CancellationToken cancellationToken = default);

    /// <summary>The approved rates in force on <paramref name="on"/>, for the reports (Q18: the QRRPA reads the quarter's last day).</summary>
    Task<LevyRateTable> InForceAsync(DateOnly on, CancellationToken cancellationToken = default);
}

/// <summary>
/// Levy rates (docs/analysis/reporting.md §4.4, §10; Q5, Q18): the basic tax, the Special Education Fund levy and the idle-land
/// tax as the LGU's ordinances set them, per municipality or for the whole province, optionally per classification,
/// effective-dated and approved by a second user. A new rate for the same keys takes over from its effective date. A
/// municipal office sets only its municipalities' rates; a province-wide rate needs the whole province in jurisdiction.
/// The rates feed report figures only (CLAUDE.md §0).
/// </summary>
public sealed class LevyRateService(
    IApplicationDbContext db,
    IValidator<CreateLevyRateRequest> validator,
    ICurrentUserService currentUser,
    IJurisdiction jurisdiction,
    IClock clock) : ILevyRateService
{
    private const string Prefix = "LEVY_RATE";

    public async Task<Result<LevyRateDto>> CreateAsync(CreateLevyRateRequest r, CancellationToken cancellationToken = default)
    {
        var validation = await validator.ValidateAsync(r, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure<LevyRateDto>("VALIDATION_FAILED", string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)));
        }
        if (r.MunicipalityId is { } m && !await db.Municipalities.AnyAsync(x => x.Id == m, cancellationToken))
        {
            return Result.Failure<LevyRateDto>("MUNICIPALITY_NOT_FOUND", "The specified municipality does not exist.");
        }
        if (r.ClassificationId is { } c && !await db.Classifications.AnyAsync(x => x.Id == c, cancellationToken))
        {
            return Result.Failure<LevyRateDto>("CLASSIFICATION_NOT_FOUND", "The specified classification does not exist.");
        }
        if (Outside(r.MunicipalityId) is { } refused)
        {
            return refused;
        }
        var rate = new LevyRate
        {
            Code = LevyRate.CodeFor(r.Kind, r.MunicipalityId, r.ClassificationId), Kind = r.Kind, MunicipalityId = r.MunicipalityId,
            ClassificationId = r.ClassificationId, RatePercent = r.RatePercent, LegalBasis = r.LegalBasis.Trim(), EffectiveDate = r.EffectiveDate,
            Description = Clean(r.Description), Remarks = Clean(r.Remarks),
        };
        db.LevyRates.Add(rate);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success(await MapAsync(rate.Id, cancellationToken));
    }

    public async Task<Result<LevyRateDto>> ApproveAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var rate = await db.LevyRates.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (rate is null)
        {
            return Result.Failure<LevyRateDto>("LEVY_RATE_NOT_FOUND", "No levy rate was found with the given id.");
        }
        if (Outside(rate.MunicipalityId) is { } refused)
        {
            return refused;
        }
        if (await ConfigurationApproval.ApproveAsync(db, currentUser, db.LevyRates.Where(x => x.Code == rate.Code), rate, Prefix, cancellationToken) is { } failure)
        {
            return Result.Failure<LevyRateDto>(failure.Code!, failure.Message!);
        }
        return Result.Success(await MapAsync(rate.Id, cancellationToken));
    }

    public async Task<Result<IReadOnlyList<LevyRateDto>>> ListAsync(bool inForceOnly, CancellationToken cancellationToken = default)
    {
        var query = Include(db.LevyRates.AsNoTracking());
        if (inForceOnly)
        {
            query = query.InForce(clock.Today);
        }
        var rows = await query.OrderBy(x => x.Kind).ThenBy(x => x.Municipality == null ? "" : x.Municipality.Name)
            .ThenBy(x => x.Classification == null ? "" : x.Classification.Name).ThenByDescending(x => x.EffectiveDate).ToListAsync(cancellationToken);
        return Result.Success<IReadOnlyList<LevyRateDto>>(rows.Select(ToDto).ToList());
    }

    public async Task<LevyRateTable> InForceAsync(DateOnly on, CancellationToken cancellationToken = default) =>
        new((await Include(db.LevyRates.AsNoTracking()).InForce(on).ToListAsync(cancellationToken)).Select(ToDto).ToList());

    /// <summary>A rate of a municipality outside the user's jurisdiction, or of the whole province for a restricted user, is refused.</summary>
    private Result<LevyRateDto>? Outside(Guid? municipalityId) => municipalityId switch
    {
        { } m when !jurisdiction.Allows(m) => Result.Failure<LevyRateDto>(JurisdictionErrors.Code, JurisdictionErrors.Message),
        null when jurisdiction.Restricted => Result.Failure<LevyRateDto>(JurisdictionErrors.Code,
            "A province-wide rate is set by an office whose jurisdiction is the whole province."),
        _ => null,
    };

    private static IQueryable<LevyRate> Include(IQueryable<LevyRate> query) => query.Include(x => x.Municipality).Include(x => x.Classification);

    private async Task<LevyRateDto> MapAsync(Guid id, CancellationToken ct) => ToDto(await Include(db.LevyRates.AsNoTracking()).SingleAsync(x => x.Id == id, ct));

    private static LevyRateDto ToDto(LevyRate x) => new(
        x.Id, x.Code, x.Kind, x.MunicipalityId, x.Municipality?.Name, x.ClassificationId, x.Classification?.Name, x.RatePercent, x.LegalBasis,
        x.EffectiveDate, x.EndDate, x.Status, x.Description, x.Remarks, x.CreatedBy, x.ApprovedBy, x.ApprovedAt);

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
