using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Prime.Application.Common;
using Prime.Application.Common.Interfaces;
using Prime.Domain.Enums;

namespace Prime.Application.Features.Assessments;

/// <summary>
/// Applies an <see cref="Domain.Entities.AssessmentLevel"/> to a
/// <see cref="Domain.Entities.Valuation"/>'s computed market value to
/// produce an assessed value (CLAUDE.md §32; ARCHITECTURE.md §3.7's
/// AssessmentService, following Phase 5's ValuationService). Never
/// recomputes the market value itself — the Valuation referenced by
/// <see cref="CreateAssessmentRequest.ValuationId"/> is the single source
/// of truth for that (Rule 9).
/// </summary>
public sealed class AssessmentService(
    IApplicationDbContext db,
    IValidator<CreateAssessmentRequest> validator,
    ICurrentUserService currentUser) : IAssessmentService
{
    public async Task<Result<AssessmentDto>> CreateAsync(CreateAssessmentRequest request, CancellationToken cancellationToken = default)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure<AssessmentDto>("VALIDATION_FAILED", string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)));
        }

        var valuation = await db.Valuations.FirstOrDefaultAsync(x => x.Id == request.ValuationId, cancellationToken);
        if (valuation is null)
        {
            return Result.Failure<AssessmentDto>("VALUATION_NOT_FOUND", "No Valuation was found with the given id.");
        }

        if (request.PreviousAssessmentId is not null
            && !await db.Assessments.AnyAsync(x => x.Id == request.PreviousAssessmentId, cancellationToken))
        {
            return Result.Failure<AssessmentDto>("PREVIOUS_ASSESSMENT_NOT_FOUND", "The specified previous assessment does not exist.");
        }

        var subject = await ResolveSubjectAsync(valuation.SourceType, valuation.SourceId, valuation.RpuId, cancellationToken);
        if (subject is null)
        {
            return Result.Failure<AssessmentDto>("TAX_DECLARATION_NOT_FOUND", "A Tax Declaration (for its classification/actual use) is required before this can be assessed.");
        }

        var propertyTypeCode = valuation.SourceType switch
        {
            ValuationSourceType.Land => PropertyTypeCodes.Land,
            ValuationSourceType.Building => PropertyTypeCodes.Building,
            ValuationSourceType.Machinery => PropertyTypeCodes.Machinery,
            _ => throw new InvalidOperationException($"Unhandled {nameof(ValuationSourceType)}: {valuation.SourceType}"),
        };
        var propertyType = await db.PropertyTypes.FirstOrDefaultAsync(x => x.Code == propertyTypeCode, cancellationToken);
        if (propertyType is null)
        {
            return Result.Failure<AssessmentDto>("PROPERTY_TYPE_NOT_CONFIGURED", $"No PropertyType with code '{propertyTypeCode}' is configured.");
        }

        var assessmentLevel = await ResolveAssessmentLevelAsync(
            subject.Value.ClassificationId, subject.Value.ActualUseId, propertyType.Id, valuation.ComputedMarketValue, request.EffectiveDate, cancellationToken);
        if (assessmentLevel is null)
        {
            return Result.Failure<AssessmentDto>("ASSESSMENT_LEVEL_NOT_FOUND", "No approved assessment level matches this valuation's classification/actual use/market-value bracket as of the effective date.");
        }

        var assessedValue = Math.Round(valuation.ComputedMarketValue * assessmentLevel.AssessmentPercentage / 100m, 2, MidpointRounding.AwayFromZero);

        var assessment = new Domain.Entities.Assessment
        {
            RpuId = valuation.RpuId,
            PropertyId = valuation.PropertyId,
            ValuationId = valuation.Id,
            AssessmentYear = request.AssessmentYear,
            MarketValue = valuation.ComputedMarketValue,
            AssessmentLevelId = assessmentLevel.Id,
            AssessmentPercentage = assessmentLevel.AssessmentPercentage,
            AssessedValue = assessedValue,
            Status = WorkflowStatus.Draft,
            EffectiveDate = request.EffectiveDate,
            PreviousAssessmentId = request.PreviousAssessmentId,
            RevisionReference = request.RevisionReference,
            Remarks = request.Remarks,
        };

        db.Assessments.Add(assessment);
        await db.SaveChangesAsync(cancellationToken);

        return Result.Success(ToDto(assessment));
    }

    public async Task<Result<AssessmentDto>> SubmitForReviewAsync(Guid assessmentId, CancellationToken cancellationToken = default)
    {
        var assessment = await db.Assessments.FirstOrDefaultAsync(x => x.Id == assessmentId, cancellationToken);
        if (assessment is null)
        {
            return Result.Failure<AssessmentDto>("ASSESSMENT_NOT_FOUND", "No Assessment was found with the given id.");
        }
        if (assessment.Status != WorkflowStatus.Draft)
        {
            return Result.Failure<AssessmentDto>("ASSESSMENT_NOT_DRAFT", "Only a Draft assessment can be submitted for review.");
        }

        assessment.Status = WorkflowStatus.PendingReview;
        await db.SaveChangesAsync(cancellationToken);

        return Result.Success(ToDto(assessment));
    }

    public async Task<Result<AssessmentDto>> ApproveAsync(Guid assessmentId, CancellationToken cancellationToken = default)
    {
        var assessment = await db.Assessments.FirstOrDefaultAsync(x => x.Id == assessmentId, cancellationToken);
        if (assessment is null)
        {
            return Result.Failure<AssessmentDto>("ASSESSMENT_NOT_FOUND", "No Assessment was found with the given id.");
        }
        if (assessment.Status != WorkflowStatus.PendingReview)
        {
            return Result.Failure<AssessmentDto>("ASSESSMENT_NOT_PENDING_REVIEW", "Only an assessment pending review can be approved.");
        }
        // Maker-checker (CLAUDE.md §46): the creator may not approve their own assessment.
        if (currentUser.AppUserId is not null && assessment.CreatedBy == currentUser.AppUserId)
        {
            return Result.Failure<AssessmentDto>("CANNOT_APPROVE_OWN_ASSESSMENT", "The assessment's creator cannot also approve it.");
        }

        assessment.Status = WorkflowStatus.Approved;
        assessment.ApprovedBy = currentUser.AppUserId;
        assessment.ApprovedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        return Result.Success(ToDto(assessment));
    }

    public async Task<Result<AssessmentDto>> RejectAsync(Guid assessmentId, string reason, CancellationToken cancellationToken = default)
    {
        var assessment = await db.Assessments.FirstOrDefaultAsync(x => x.Id == assessmentId, cancellationToken);
        if (assessment is null)
        {
            return Result.Failure<AssessmentDto>("ASSESSMENT_NOT_FOUND", "No Assessment was found with the given id.");
        }
        if (assessment.Status != WorkflowStatus.PendingReview)
        {
            return Result.Failure<AssessmentDto>("ASSESSMENT_NOT_PENDING_REVIEW", "Only an assessment pending review can be rejected.");
        }

        assessment.Status = WorkflowStatus.Rejected;
        currentUser.Reason = reason;
        await db.SaveChangesAsync(cancellationToken);

        return Result.Success(ToDto(assessment));
    }

    public async Task<Result<AssessmentDto>> PostAsync(Guid assessmentId, CancellationToken cancellationToken = default)
    {
        var assessment = await db.Assessments.FirstOrDefaultAsync(x => x.Id == assessmentId, cancellationToken);
        if (assessment is null)
        {
            return Result.Failure<AssessmentDto>("ASSESSMENT_NOT_FOUND", "No Assessment was found with the given id.");
        }
        if (assessment.Status != WorkflowStatus.Approved)
        {
            return Result.Failure<AssessmentDto>("ASSESSMENT_NOT_APPROVED", "Only an approved assessment can be posted.");
        }

        assessment.Status = WorkflowStatus.Posted;
        await db.SaveChangesAsync(cancellationToken);

        return Result.Success(ToDto(assessment));
    }

    public async Task<Result<AssessmentDto>> GetByIdAsync(Guid assessmentId, CancellationToken cancellationToken = default)
    {
        var assessment = await db.Assessments.FirstOrDefaultAsync(x => x.Id == assessmentId, cancellationToken);
        return assessment is null
            ? Result.Failure<AssessmentDto>("ASSESSMENT_NOT_FOUND", "No Assessment was found with the given id.")
            : Result.Success(ToDto(assessment));
    }

    public async Task<Result<IReadOnlyList<AssessmentDto>>> ListByRpuAsync(Guid rpuId, DateOnly? asOfDate, CancellationToken cancellationToken = default)
    {
        var query = db.Assessments.Where(x => x.RpuId == rpuId);
        if (asOfDate is not null)
        {
            query = query.Where(x => x.EffectiveDate <= asOfDate);
        }

        var assessments = await query.OrderByDescending(x => x.EffectiveDate).ToListAsync(cancellationToken);
        return Result.Success<IReadOnlyList<AssessmentDto>>(assessments.Select(ToDto).ToList());
    }

    /// <summary>
    /// Land carries its own Classification/ActualUse; Building/Machinery
    /// don't, so they're resolved from the RPU's current Tax Declaration —
    /// same gap and same fix as ValuationService.ComputeForBuildingAsync.
    /// Returns null when a Building/Machinery has no Tax Declaration yet.
    /// </summary>
    private async Task<(Guid ClassificationId, Guid ActualUseId)?> ResolveSubjectAsync(
        ValuationSourceType sourceType, Guid sourceId, Guid rpuId, CancellationToken cancellationToken)
    {
        if (sourceType == ValuationSourceType.Land)
        {
            var land = await db.Lands.FirstOrDefaultAsync(x => x.Id == sourceId, cancellationToken);
            return land is null ? null : (land.ClassificationId, land.ActualUseId);
        }

        var taxDeclaration = await TaxDeclarationLookup.GetCurrentAsync(db, rpuId, cancellationToken);
        return taxDeclaration is null ? null : (taxDeclaration.ClassificationId, taxDeclaration.ActualUseId);
    }

    private async Task<Domain.Entities.AssessmentLevel?> ResolveAssessmentLevelAsync(
        Guid classificationId, Guid actualUseId, Guid propertyTypeId, decimal marketValue, DateOnly asOf, CancellationToken cancellationToken)
    {
        return await db.AssessmentLevels
            .Where(x => x.ClassificationId == classificationId
                && x.ActualUseId == actualUseId
                && x.PropertyTypeId == propertyTypeId
                && x.Status == WorkflowStatus.Approved
                && x.EffectiveDate <= asOf
                && (x.EndDate == null || x.EndDate > asOf)
                && x.LowerValue <= marketValue
                && (x.UpperValue == null || x.UpperValue >= marketValue))
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static AssessmentDto ToDto(Domain.Entities.Assessment x) => new(
        x.Id,
        x.RpuId,
        x.PropertyId,
        x.ValuationId,
        x.AssessmentYear,
        x.MarketValue,
        x.AssessmentLevelId,
        x.AssessmentPercentage,
        x.AssessedValue,
        x.Status,
        x.EffectiveDate,
        x.PreviousAssessmentId,
        x.RevisionReference,
        x.Remarks,
        x.ApprovedBy,
        x.ApprovedAt,
        x.CreatedAt);
}
