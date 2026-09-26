using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Prime.Application.Common;
using Prime.Application.Common.Interfaces;
using Prime.Domain.Enums;

namespace Prime.Application.Features.AssessmentLevels;

public sealed class AssessmentLevelService(
    IApplicationDbContext db,
    IValidator<CreateAssessmentLevelRequest> validator,
    ICurrentUserService currentUser) : IAssessmentLevelService
{
    public async Task<Result<AssessmentLevelDto>> CreateAsync(CreateAssessmentLevelRequest request, CancellationToken cancellationToken = default)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure<AssessmentLevelDto>("VALIDATION_FAILED", string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)));
        }

        if (!await db.Classifications.AnyAsync(x => x.Id == request.ClassificationId, cancellationToken))
        {
            return Result.Failure<AssessmentLevelDto>("CLASSIFICATION_NOT_FOUND", "The specified classification does not exist.");
        }
        if (!await db.ActualUses.AnyAsync(x => x.Id == request.ActualUseId, cancellationToken))
        {
            return Result.Failure<AssessmentLevelDto>("ACTUAL_USE_NOT_FOUND", "The specified actual use does not exist.");
        }
        if (!await db.PropertyTypes.AnyAsync(x => x.Id == request.PropertyTypeId, cancellationToken))
        {
            return Result.Failure<AssessmentLevelDto>("PROPERTY_TYPE_NOT_FOUND", "The specified property type does not exist.");
        }

        // Never overwrite (CLAUDE.md §29): a new level closes the open levels of the same
        // classification/actual use/property type whose value range it overlaps. Levels with
        // separate ranges — the brackets of one table — stay open side by side
        // (docs/analysis/value-and-assess.md §2.5).
        var open = await db.AssessmentLevels
            .Where(x => x.ClassificationId == request.ClassificationId
                && x.ActualUseId == request.ActualUseId
                && x.PropertyTypeId == request.PropertyTypeId
                && x.EndDate == null)
            .ToListAsync(cancellationToken);
        foreach (var currentlyOpen in open.Where(x => RangesOverlap(x.LowerValue, x.UpperValue, request.LowerValue, request.UpperValue)))
        {
            if (request.EffectiveDate <= currentlyOpen.EffectiveDate)
            {
                return Result.Failure<AssessmentLevelDto>(
                    "ASSESSMENT_LEVEL_EFFECTIVE_DATE_CONFLICT",
                    "The new assessment level's effective date must be after that of the open level whose value range it overlaps.");
            }
            currentlyOpen.EndDate = request.EffectiveDate.AddDays(-1);
        }

        var assessmentLevel = new Domain.Entities.AssessmentLevel
        {
            OrdinanceNumber = request.OrdinanceNumber,
            OrdinanceDate = request.OrdinanceDate,
            ClassificationId = request.ClassificationId,
            ActualUseId = request.ActualUseId,
            PropertyTypeId = request.PropertyTypeId,
            LowerValue = request.LowerValue,
            UpperValue = request.UpperValue,
            AssessmentPercentage = request.AssessmentPercentage,
            EffectiveDate = request.EffectiveDate,
            Status = WorkflowStatus.Draft,
        };

        db.AssessmentLevels.Add(assessmentLevel);
        await db.SaveChangesAsync(cancellationToken);

        return Result.Success(await MapToDto(assessmentLevel.Id, cancellationToken)
            ?? throw new InvalidOperationException("Assessment level was just created but could not be reloaded."));
    }

    public async Task<Result<AssessmentLevelDto>> ApproveAsync(Guid assessmentLevelId, CancellationToken cancellationToken = default)
    {
        var assessmentLevel = await db.AssessmentLevels.FirstOrDefaultAsync(x => x.Id == assessmentLevelId, cancellationToken);
        if (assessmentLevel is null)
        {
            return Result.Failure<AssessmentLevelDto>("ASSESSMENT_LEVEL_NOT_FOUND", "No assessment level was found with the given id.");
        }
        if (assessmentLevel.Status is WorkflowStatus.Approved or WorkflowStatus.Posted)
        {
            return Result.Failure<AssessmentLevelDto>("ASSESSMENT_LEVEL_ALREADY_APPROVED", "This assessment level has already been approved.");
        }
        // Maker-checker (CLAUDE.md §46): the creator may not approve their own assessment level.
        if (currentUser.AppUserId is not null && assessmentLevel.CreatedBy == currentUser.AppUserId)
        {
            return Result.Failure<AssessmentLevelDto>("CANNOT_APPROVE_OWN_ASSESSMENT_LEVEL", "The assessment level's creator cannot also approve it.");
        }

        // Two approved levels may never both apply to one value on one day.
        var siblings = await db.AssessmentLevels
            .Where(x => x.Id != assessmentLevel.Id
                && x.ClassificationId == assessmentLevel.ClassificationId
                && x.ActualUseId == assessmentLevel.ActualUseId
                && x.PropertyTypeId == assessmentLevel.PropertyTypeId
                && x.Status == WorkflowStatus.Approved
                && (x.EndDate == null || x.EndDate >= assessmentLevel.EffectiveDate)
                && (assessmentLevel.EndDate == null || x.EffectiveDate <= assessmentLevel.EndDate))
            .ToListAsync(cancellationToken);
        if (siblings.Any(x => RangesOverlap(x.LowerValue, x.UpperValue, assessmentLevel.LowerValue, assessmentLevel.UpperValue)))
        {
            return Result.Failure<AssessmentLevelDto>("ASSESSMENT_LEVEL_OVERLAP",
                "An approved level of the same classification, actual use and property type already covers part of this value range in this period.");
        }

        assessmentLevel.Status = WorkflowStatus.Approved;
        assessmentLevel.ApprovedBy = currentUser.AppUserId;
        assessmentLevel.ApprovedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        return Result.Success(await MapToDto(assessmentLevel.Id, cancellationToken)
            ?? throw new InvalidOperationException("Assessment level was just approved but could not be reloaded."));
    }

    public async Task<Result<AssessmentLevelDto>> GetByIdAsync(Guid assessmentLevelId, CancellationToken cancellationToken = default)
    {
        var dto = await MapToDto(assessmentLevelId, cancellationToken);
        return dto is null
            ? Result.Failure<AssessmentLevelDto>("ASSESSMENT_LEVEL_NOT_FOUND", "No assessment level was found with the given id.")
            : Result.Success(dto);
    }

    public async Task<Result<IReadOnlyList<AssessmentLevelDto>>> ListAsync(CancellationToken cancellationToken = default)
    {
        var entities = await IncludeReferences(db.AssessmentLevels)
            .OrderByDescending(x => x.EffectiveDate)
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<AssessmentLevelDto>>(entities.Select(ProjectToDto).ToList());
    }

    private async Task<AssessmentLevelDto?> MapToDto(Guid id, CancellationToken cancellationToken)
    {
        var entity = await IncludeReferences(db.AssessmentLevels).SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        return entity is null ? null : ProjectToDto(entity);
    }

    private static IQueryable<Domain.Entities.AssessmentLevel> IncludeReferences(IQueryable<Domain.Entities.AssessmentLevel> query) => query
        .Include(x => x.Classification)
        .Include(x => x.ActualUse)
        .Include(x => x.PropertyType);

    private static AssessmentLevelDto ProjectToDto(Domain.Entities.AssessmentLevel x) => new(
        x.Id,
        x.OrdinanceNumber,
        x.OrdinanceDate,
        x.ClassificationId,
        x.Classification!.Name,
        x.ActualUseId,
        x.ActualUse!.Name,
        x.PropertyTypeId,
        x.PropertyType!.Name,
        x.LowerValue,
        x.UpperValue,
        x.AssessmentPercentage,
        x.EffectiveDate,
        x.EndDate,
        x.Status,
        x.CreatedAt);

    /// <summary>
    /// Whether two value ranges share a value, each read "over the lower, not over the upper"
    /// (a null upper is unbounded) — the matching the assessment uses.
    /// </summary>
    public static bool RangesOverlap(decimal lower1, decimal? upper1, decimal lower2, decimal? upper2) =>
        (upper2 is null || lower1 < upper2) && (upper1 is null || lower2 < upper1);
}
