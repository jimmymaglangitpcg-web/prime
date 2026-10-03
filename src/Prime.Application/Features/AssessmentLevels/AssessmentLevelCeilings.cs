using System.Globalization;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Prime.Application.Common;
using Prime.Application.Common.Interfaces;
using Prime.Domain.Entities;
using Prime.Domain.Enums;

namespace Prime.Application.Features.AssessmentLevels;

public sealed record CreateAssessmentLevelCeilingRequest(
    string LegalBasis, DateOnly EffectiveDate, string? Remarks, string Code, string? Description,
    Guid PropertyTypeId, Guid? ClassificationId, Guid? ActualUseId, decimal LowerValue, decimal? UpperValue, decimal MaximumPercentage);

/// <param name="Warning">Set by approval only: approved levels in force that are above the new ceiling.</param>
public sealed record AssessmentLevelCeilingDto(
    Guid Id, string Code, string? Description, Guid PropertyTypeId, string PropertyTypeName, Guid? ClassificationId, string? ClassificationName,
    Guid? ActualUseId, string? ActualUseName, decimal LowerValue, decimal? UpperValue, decimal MaximumPercentage,
    string LegalBasis, DateOnly EffectiveDate, DateOnly? EndDate, WorkflowStatus Status, Guid? CreatedBy, Guid? ApprovedBy, DateTimeOffset? ApprovedAt,
    string? Remarks, string? Warning = null);

public sealed class CreateAssessmentLevelCeilingRequestValidator : AbstractValidator<CreateAssessmentLevelCeilingRequest>
{
    public CreateAssessmentLevelCeilingRequestValidator()
    {
        RuleFor(x => x.LegalBasis).NotEmpty().MaximumLength(500);
        RuleFor(x => x.EffectiveDate).NotEqual(default(DateOnly)).WithMessage("effectiveDate is required.");
        RuleFor(x => x.Remarks).MaximumLength(1000);
        RuleFor(x => x.Code).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Description).MaximumLength(1000);
        RuleFor(x => x.PropertyTypeId).NotEmpty();
        RuleFor(x => x.LowerValue).GreaterThanOrEqualTo(0m);
        RuleFor(x => x.UpperValue).GreaterThan(x => x.LowerValue).When(x => x.UpperValue is not null);
        RuleFor(x => x.MaximumPercentage).GreaterThan(0m).LessThanOrEqualTo(100m);
    }
}

public interface IAssessmentLevelCeilingService
{
    Task<Result<AssessmentLevelCeilingDto>> CreateAsync(CreateAssessmentLevelCeilingRequest request, CancellationToken cancellationToken = default);
    Task<Result<AssessmentLevelCeilingDto>> ApproveAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<AssessmentLevelCeilingDto>>> ListAsync(bool inForceOnly, CancellationToken cancellationToken = default);
}

/// <summary>
/// Statutory maximum assessment levels (docs/analysis/assessment-listing-exemptions.md §4.2, step L3-2): optional,
/// effective-dated configuration loaded from the content pack, approved by a second user. With none configured,
/// assessment levels are not checked.
/// </summary>
public sealed class AssessmentLevelCeilingService(
    IApplicationDbContext db,
    IValidator<CreateAssessmentLevelCeilingRequest> validator,
    ICurrentUserService currentUser,
    IClock clock) : IAssessmentLevelCeilingService
{
    public async Task<Result<AssessmentLevelCeilingDto>> CreateAsync(CreateAssessmentLevelCeilingRequest r, CancellationToken cancellationToken = default)
    {
        var validation = await validator.ValidateAsync(r, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure<AssessmentLevelCeilingDto>("VALIDATION_FAILED", string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)));
        }
        if (!await db.PropertyTypes.AnyAsync(x => x.Id == r.PropertyTypeId, cancellationToken))
        {
            return Result.Failure<AssessmentLevelCeilingDto>("PROPERTY_TYPE_NOT_FOUND", "The specified property type does not exist.");
        }
        if (r.ClassificationId is { } c && !await db.Classifications.AnyAsync(x => x.Id == c, cancellationToken))
        {
            return Result.Failure<AssessmentLevelCeilingDto>("CLASSIFICATION_NOT_FOUND", "The specified classification does not exist.");
        }
        if (r.ActualUseId is { } u && !await db.ActualUses.AnyAsync(x => x.Id == u, cancellationToken))
        {
            return Result.Failure<AssessmentLevelCeilingDto>("ACTUAL_USE_NOT_FOUND", "The specified actual use does not exist.");
        }
        var ceiling = new AssessmentLevelCeiling
        {
            LegalBasis = r.LegalBasis.Trim(), EffectiveDate = r.EffectiveDate, Remarks = Clean(r.Remarks), Code = r.Code.Trim(), Description = Clean(r.Description),
            PropertyTypeId = r.PropertyTypeId, ClassificationId = r.ClassificationId, ActualUseId = r.ActualUseId,
            LowerValue = r.LowerValue, UpperValue = r.UpperValue, MaximumPercentage = r.MaximumPercentage,
        };
        db.AssessmentLevelCeilings.Add(ceiling);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success(await MapAsync(ceiling.Id, cancellationToken));
    }

    public async Task<Result<AssessmentLevelCeilingDto>> ApproveAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var ceiling = await db.AssessmentLevelCeilings.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (ceiling is null)
        {
            return Result.Failure<AssessmentLevelCeilingDto>("ASSESSMENT_LEVEL_CEILING_NOT_FOUND", "No assessment-level ceiling was found with the given id.");
        }
        if (await ConfigurationApproval.ApproveAsync(db, currentUser, db.AssessmentLevelCeilings.Where(x => x.Code == ceiling.Code),
                ceiling, "ASSESSMENT_LEVEL_CEILING", cancellationToken) is { } failure)
        {
            return Result.Failure<AssessmentLevelCeilingDto>(failure.Code!, failure.Message!);
        }
        // The law may lower a maximum under levels already in force: say so, do not change them (they are the LGU's ordinance).
        var from = ceiling.EffectiveDate > clock.Today ? ceiling.EffectiveDate : clock.Today;
        var above = (await db.AssessmentLevels.AsNoTracking()
                .Where(x => x.Status == WorkflowStatus.Approved && x.PropertyTypeId == ceiling.PropertyTypeId
                    && (ceiling.ClassificationId == null || x.ClassificationId == ceiling.ClassificationId)
                    && (ceiling.ActualUseId == null || x.ActualUseId == ceiling.ActualUseId)
                    && x.AssessmentPercentage > ceiling.MaximumPercentage && (x.EndDate == null || x.EndDate >= from))
                .Select(x => new { x.LowerValue, x.UpperValue, x.OrdinanceNumber, x.AssessmentPercentage }).ToListAsync(cancellationToken))
            .Where(x => AssessmentLevelService.RangesOverlap(x.LowerValue, x.UpperValue, ceiling.LowerValue, ceiling.UpperValue))
            .ToList();
        var warning = above.Count == 0 ? null
            : $"{above.Count} approved assessment level(s) in force from {from:yyyy-MM-dd} are above this {Pct(ceiling.MaximumPercentage)} ceiling "
              + $"(ordinance {string.Join(", ", above.Select(a => a.OrdinanceNumber).Distinct())}); they are not changed: a new level must replace them.";
        return Result.Success(await MapAsync(ceiling.Id, cancellationToken) with { Warning = warning });
    }

    public async Task<Result<IReadOnlyList<AssessmentLevelCeilingDto>>> ListAsync(bool inForceOnly, CancellationToken cancellationToken = default)
    {
        var query = Include(db.AssessmentLevelCeilings.AsNoTracking());
        if (inForceOnly)
        {
            query = query.InForce(clock.Today);
        }
        var rows = await query.OrderBy(x => x.Code).ThenByDescending(x => x.EffectiveDate).ToListAsync(cancellationToken);
        return Result.Success<IReadOnlyList<AssessmentLevelCeilingDto>>(rows.Select(ToDto).ToList());
    }

    /// <summary>
    /// Why an assessment level of these keys and percentage, effective on <paramref name="effective"/>, is not allowed:
    /// the lowest ceiling in force that day for its property type (and its classification or actual use, where the
    /// ceiling names one) whose bracket overlaps the level's, when the level is above it. Null when allowed.
    /// </summary>
    internal static async Task<string?> ViolationAsync(IApplicationDbContext db, Guid propertyTypeId, Guid classificationId, Guid actualUseId,
        decimal lower, decimal? upper, decimal percentage, DateOnly effective, CancellationToken ct)
    {
        var ceilings = (await Include(db.AssessmentLevelCeilings.AsNoTracking()).InForce(effective)
                .Where(x => x.PropertyTypeId == propertyTypeId && (x.ClassificationId == null || x.ClassificationId == classificationId)
                    && (x.ActualUseId == null || x.ActualUseId == actualUseId) && x.MaximumPercentage < percentage)
                .ToListAsync(ct))
            .Where(x => AssessmentLevelService.RangesOverlap(x.LowerValue, x.UpperValue, lower, upper))
            .OrderBy(x => x.MaximumPercentage).ToList();
        if (ceilings.FirstOrDefault() is not { } c)
        {
            return null;
        }
        var keys = string.Join(" / ", new[] { c.PropertyType!.Name, c.Classification?.Name, c.ActualUse?.Name }.Where(x => x is not null));
        return $"The level of {Pct(percentage)} is above the maximum of {Pct(c.MaximumPercentage)} for {keys} "
            + $"({Money(c.LowerValue)} – {(c.UpperValue is { } u ? Money(u) : "up")}) under {c.LegalBasis} (ceiling {c.Code}, effective {c.EffectiveDate:yyyy-MM-dd}).";
    }

    private static IQueryable<AssessmentLevelCeiling> Include(IQueryable<AssessmentLevelCeiling> query) =>
        query.Include(x => x.PropertyType).Include(x => x.Classification).Include(x => x.ActualUse);

    private async Task<AssessmentLevelCeilingDto> MapAsync(Guid id, CancellationToken ct) =>
        ToDto(await Include(db.AssessmentLevelCeilings.AsNoTracking()).SingleAsync(x => x.Id == id, ct));

    private static AssessmentLevelCeilingDto ToDto(AssessmentLevelCeiling x) => new(
        x.Id, x.Code, x.Description, x.PropertyTypeId, x.PropertyType!.Name, x.ClassificationId, x.Classification?.Name, x.ActualUseId, x.ActualUse?.Name,
        x.LowerValue, x.UpperValue, x.MaximumPercentage, x.LegalBasis, x.EffectiveDate, x.EndDate, x.Status, x.CreatedBy, x.ApprovedBy, x.ApprovedAt, x.Remarks);

    private static string Pct(decimal v) => v.ToString("0.######", CultureInfo.InvariantCulture) + "%";

    private static string Money(decimal v) => v.ToString("#,0.00", CultureInfo.InvariantCulture);

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
