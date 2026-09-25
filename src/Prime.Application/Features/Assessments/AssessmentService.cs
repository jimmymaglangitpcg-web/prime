using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Prime.Application.Common;
using Prime.Application.Common.Interfaces;
using Prime.Application.Features.Approvals;
using Prime.Application.Features.Numbering;
using Prime.Application.Features.TaxDeclarations;
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
    ICurrentUserService currentUser,
    IApprovalChainService approvals,
    INumberingService numbering,
    IClock clock,
    IOptions<FaasOptions> faas,
    IOptions<AssessmentOptions> options,
    ILogger<AssessmentService> logger) : IAssessmentService
{
    public async Task<Result<AssessmentDto>> CreateAsync(CreateAssessmentRequest request, CancellationToken cancellationToken = default)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure<AssessmentDto>("VALIDATION_FAILED", string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)));
        }

        var valuation = await db.Valuations.Include(x => x.Lines).FirstOrDefaultAsync(x => x.Id == request.ValuationId, cancellationToken);
        if (valuation is null)
        {
            return Result.Failure<AssessmentDto>("VALUATION_NOT_FOUND", "No Valuation was found with the given id.");
        }

        if (request.PreviousAssessmentId is not null
            && !await db.Assessments.AnyAsync(x => x.Id == request.PreviousAssessmentId, cancellationToken))
        {
            return Result.Failure<AssessmentDto>("PREVIOUS_ASSESSMENT_NOT_FOUND", "The specified previous assessment does not exist.");
        }

        var lines = valuation.Lines.OrderBy(x => x.Sequence).ToList();
        if (lines.Count == 0)
        {
            return Result.Failure<AssessmentDto>("VALUATION_HAS_NO_LINES", "The valuation has no appraisal lines to assess.");
        }
        // A line without its own classification or actual use takes the unit's Tax Declaration's.
        Domain.Entities.TaxDeclaration? taxDeclaration = null;
        if (lines.Any(l => l.ClassificationId is null || l.ActualUseId is null))
        {
            taxDeclaration = await TaxDeclarationLookup.GetCurrentAsync(db, valuation.RpuId, cancellationToken);
            if (taxDeclaration is null)
            {
                return Result.Failure<AssessmentDto>("TAX_DECLARATION_NOT_FOUND", "A Tax Declaration (for its classification/actual use) is required before this can be assessed.");
            }
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

        // The FAAS "Property Assessment" rows: valuation lines grouped by (classification, actual use),
        // each with its own level (docs/analysis/mrpaao-forms-model.md §8.2).
        var groups = lines
            .GroupBy(l => (Classification: l.ClassificationId ?? taxDeclaration!.ClassificationId, ActualUse: l.ActualUseId ?? taxDeclaration!.ActualUseId))
            .ToList();
        var unitMarketValue = lines.Sum(l => l.MarketValue);
        var assessmentLines = new List<Domain.Entities.AssessmentLine>();
        foreach (var group in groups)
        {
            var marketValue = group.Sum(l => l.MarketValue);
            var bracketValue = options.Value.LevelBracketBasis == LevelBracketBasis.Unit ? unitMarketValue : marketValue;
            var level = await ResolveAssessmentLevelAsync(group.Key.Classification, group.Key.ActualUse, propertyType.Id, bracketValue, request.EffectiveDate, cancellationToken);
            if (level is null)
            {
                var names = await db.Classifications.Where(x => x.Id == group.Key.Classification).Select(x => x.Name).FirstOrDefaultAsync(cancellationToken)
                    + " / " + await db.ActualUses.Where(x => x.Id == group.Key.ActualUse).Select(x => x.Name).FirstOrDefaultAsync(cancellationToken);
                return Result.Failure<AssessmentDto>("ASSESSMENT_LEVEL_NOT_FOUND",
                    $"No approved assessment level matches {names} ({propertyType.Name}) for a market value of {bracketValue:#,0.00} as of the effective date.");
            }
            assessmentLines.Add(new Domain.Entities.AssessmentLine
            {
                Sequence = assessmentLines.Count + 1,
                ClassificationId = group.Key.Classification,
                ActualUseId = group.Key.ActualUse,
                PropertyTypeId = propertyType.Id,
                MarketValue = marketValue,
                AssessmentLevelId = level.Id,
                AssessmentPercentage = level.AssessmentPercentage,
                AssessedValue = Math.Round(marketValue * level.AssessmentPercentage / 100m, 2, MidpointRounding.AwayFromZero),
            });
        }
        var single = assessmentLines.Count == 1 ? assessmentLines[0] : null;

        var assessment = new Domain.Entities.Assessment
        {
            RpuId = valuation.RpuId,
            PropertyId = valuation.PropertyId,
            ValuationId = valuation.Id,
            AssessmentYear = request.AssessmentYear,
            MarketValue = assessmentLines.Sum(l => l.MarketValue),
            AssessmentLevelId = single?.AssessmentLevelId,
            AssessmentPercentage = single?.AssessmentPercentage,
            AssessedValue = assessmentLines.Sum(l => l.AssessedValue),
            Status = WorkflowStatus.Draft,
            EffectiveDate = request.EffectiveDate,
            PreviousAssessmentId = request.PreviousAssessmentId,
            RevisionReference = request.RevisionReference,
            Remarks = request.Remarks,
            Lines = assessmentLines,
        };

        db.Assessments.Add(assessment);
        await db.SaveChangesAsync(cancellationToken);

        return Result.Success(await MapAsync(assessment.Id, cancellationToken));
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

        return Result.Success(await MapAsync(assessment.Id, cancellationToken));
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
        // A configured approval chain (docs/FORMS-REVISION-PLAN.md §4.5): each call signs the
        // next step; the assessment is Approved when the last step is signed.
        var step = await approvals.SignNextStepAsync(ApprovalSubjectType.Assessment, assessment.Id, assessment.CreatedBy,
            clock.Today, null, cancellationToken);
        if (step.IsFailure)
        {
            return Result.Failure<AssessmentDto>(step.Code!, step.Message!);
        }
        if (!step.Value.ChainInForce)
        {
            // No chain configured — maker-checker (CLAUDE.md §46): the creator may not approve their own assessment.
            if (currentUser.AppUserId is not null && assessment.CreatedBy == currentUser.AppUserId)
            {
                return Result.Failure<AssessmentDto>("CANNOT_APPROVE_OWN_ASSESSMENT", "The assessment's creator cannot also approve it.");
            }
        }
        if (!step.Value.ChainInForce || step.Value.Completed)
        {
            assessment.Status = WorkflowStatus.Approved;
            assessment.ApprovedBy = currentUser.AppUserId;
            assessment.ApprovedAt = DateTimeOffset.UtcNow;
            // With FAAS numbers of their own, number the appraisal record once approved, if a FAAS
            // scheme is in force. By default the FAAS number is the TD's (docs/analysis/mrpaao-forms-model.md §6.1).
            if (faas.Value.NumberSource == FaasNumberSource.Own)
            {
                var context = await NumberContexts.ForPropertyAsync(db, assessment.PropertyId, assessment.AssessmentYear, cancellationToken);
                var faasNumber = await numbering.GenerateIfConfiguredAsync(NumberedDocumentKind.Faas, context, clock.Today, cancellationToken);
                if (faasNumber.IsFailure)
                {
                    return Result.Failure<AssessmentDto>(faasNumber.Code!, faasNumber.Message!);
                }
                assessment.FaasNumber = faasNumber.Value;
            }
        }
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // IX_ApprovalRecords_SubjectType_SubjectId_StepSequence: someone signed this step at the same time.
            return Result.Failure<AssessmentDto>("APPROVAL_STEP_CONFLICT", "This approval step was signed by someone else at the same time. Reload and try again.");
        }

        return Result.Success(await MapAsync(assessment.Id, cancellationToken));
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

        return Result.Success(await MapAsync(assessment.Id, cancellationToken));
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
        var ownsTransaction = db.Database.CurrentTransaction is null;
        await using var transaction = ownsTransaction ? await db.Database.BeginTransactionAsync(cancellationToken) : null;
        if (faas.Value.PrepareTdOnPosting)
        {
            // The new FAAS: a Draft TD declaring this assessment, for the assessor to review and approve.
            if (await FaasTaxDeclarations.PrepareForPostedAsync(db, numbering, assessment, clock.Today, cancellationToken) is { } skipped)
            {
                logger.LogInformation("No Tax Declaration prepared for posted assessment {AssessmentId}: {Reason}", assessment.Id, skipped);
            }
        }
        await db.SaveChangesAsync(cancellationToken);
        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        return Result.Success(await MapAsync(assessment.Id, cancellationToken));
    }

    public async Task<Result<AssessmentDto>> GetByIdAsync(Guid assessmentId, CancellationToken cancellationToken = default)
    {
        var assessment = await db.Assessments.FirstOrDefaultAsync(x => x.Id == assessmentId, cancellationToken);
        return assessment is null
            ? Result.Failure<AssessmentDto>("ASSESSMENT_NOT_FOUND", "No Assessment was found with the given id.")
            : Result.Success(await MapAsync(assessment.Id, cancellationToken));
    }

    public async Task<Result<IReadOnlyList<AssessmentDto>>> ListByRpuAsync(Guid rpuId, DateOnly? asOfDate, CancellationToken cancellationToken = default)
    {
        var query = db.Assessments.Where(x => x.RpuId == rpuId);
        if (asOfDate is not null)
        {
            query = query.Where(x => x.EffectiveDate <= asOfDate);
        }

        var assessments = await WithLines(query).AsNoTracking().OrderByDescending(x => x.EffectiveDate).ToListAsync(cancellationToken);
        return Result.Success<IReadOnlyList<AssessmentDto>>(assessments.Select(ToDto).ToList());
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

    private static IQueryable<Domain.Entities.Assessment> WithLines(IQueryable<Domain.Entities.Assessment> query) => query
        .Include(x => x.Lines).ThenInclude(l => l.Classification)
        .Include(x => x.Lines).ThenInclude(l => l.ActualUse);

    private async Task<AssessmentDto> MapAsync(Guid id, CancellationToken ct) =>
        ToDto(await WithLines(db.Assessments).AsNoTracking().SingleAsync(x => x.Id == id, ct));

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
        x.FaasNumber,
        x.ApprovedBy,
        x.ApprovedAt,
        x.CreatedAt,
        x.Lines.OrderBy(l => l.Sequence).Select(l => new AssessmentLineDto(l.Id, l.Sequence, l.ClassificationId, l.Classification!.Name,
            l.ActualUseId, l.ActualUse!.Name, l.MarketValue, l.AssessmentLevelId, l.AssessmentPercentage, l.AssessedValue)).ToList());
}
