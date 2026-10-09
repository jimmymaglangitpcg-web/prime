using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Prime.Application.Common;
using Prime.Application.Common.Interfaces;
using Prime.Application.Features.Approvals;
using Prime.Application.Features.Exemptions;
using Prime.Application.Features.Numbering;
using Prime.Application.Features.TaxDeclarations;
using Prime.Domain.DomainServices;
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
        var calculated = await CalculateAsync(request, cancellationToken);
        if (calculated.IsFailure)
        {
            return Result.Failure<AssessmentDto>(calculated.Code!, calculated.Message!);
        }
        var (valuation, assessmentLines, effectivity) = calculated.Value;
        var single = assessmentLines.Count == 1 ? assessmentLines[0] : null;

        var assessment = new Domain.Entities.Assessment
        {
            RpuId = valuation.RpuId,
            PropertyId = valuation.PropertyId,
            ValuationId = valuation.Id,
            AssessmentYear = request.AssessmentYear ?? effectivity.Year,
            MarketValue = assessmentLines.Sum(l => l.MarketValue),
            AssessmentLevelId = single?.AssessmentLevelId,
            AssessmentPercentage = single?.AssessmentPercentage,
            AssessedValue = assessmentLines.Sum(l => l.AssessedValue),
            Status = WorkflowStatus.Draft,
            EffectiveDate = effectivity.EffectiveDate,
            TransactionTypeId = effectivity.TransactionTypeId,
            TransactionCode = effectivity.TransactionCode,
            EffectivityRule = effectivity.Rule,
            CauseDate = effectivity.CauseDate,
            CauseWindowDays = effectivity.CauseWindowDays,
            CauseWindowExceeded = effectivity.CauseWindowExceeded,
            EffectivityOverrideReason = effectivity.Overridden ? request.EffectivityOverrideReason!.Trim() : null,
            PreviousAssessmentId = request.PreviousAssessmentId,
            RevisionReference = request.RevisionReference,
            Remarks = request.Remarks,
            Lines = assessmentLines,
        };

        db.Assessments.Add(assessment);
        await db.SaveChangesAsync(cancellationToken);

        return Result.Success(await MapAsync(assessment.Id, cancellationToken));
    }

    /// <summary>The rows an assessment of this valuation would have, without saving (docs/analysis/value-and-assess.md §2.3).</summary>
    public async Task<Result<AssessmentPreviewDto>> PreviewAsync(CreateAssessmentRequest request, CancellationToken cancellationToken = default)
    {
        var calculated = await CalculateAsync(request, cancellationToken);
        if (calculated.IsFailure)
        {
            return Result.Failure<AssessmentPreviewDto>(calculated.Code!, calculated.Message!);
        }
        var (valuation, lines, effectivity) = calculated.Value;
        var classificationIds = lines.Select(l => l.ClassificationId).Distinct().ToList();
        var actualUseIds = lines.Select(l => l.ActualUseId).Distinct().ToList();
        var classifications = await db.Classifications.Where(x => classificationIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);
        var actualUses = await db.ActualUses.Where(x => actualUseIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);
        return Result.Success(new AssessmentPreviewDto(valuation.Id, valuation.RpuId, lines.Sum(l => l.MarketValue), lines.Sum(l => l.AssessedValue),
            lines.Select(l => new AssessmentLineDto(Guid.Empty, l.Sequence, l.ClassificationId, classifications.GetValueOrDefault(l.ClassificationId, ""),
                l.ActualUseId, actualUses.GetValueOrDefault(l.ActualUseId, ""), l.MarketValue, l.AssessmentLevelId, l.AssessmentPercentage, l.AssessedValue,
                l.Taxability, l.PropertyExemptionId, l.TaxabilityNote)).ToList(),
            effectivity));
    }

    public Task<Result<EffectivityDto>> EffectivityAsync(Guid? transactionTypeId, DateOnly? causeDate, DateOnly? effectiveDate, string? overrideReason,
        CancellationToken cancellationToken = default) =>
        ResolveEffectivityAsync(transactionTypeId, causeDate, effectiveDate, overrideReason, revision: false, clock.Today, cancellationToken);

    /// <summary>
    /// The effectivity of an assessment made on <paramref name="madeOn"/> (docs/analysis/valuation-foundation.md
    /// §4.2): derived by the transaction type's rule, or given by the caller where no rule derives it. A general
    /// revision's date is fixed by the revision, under the configured general-revision code.
    /// </summary>
    private async Task<Result<EffectivityDto>> ResolveEffectivityAsync(Guid? transactionTypeId, DateOnly? causeDate, DateOnly? effectiveDate,
        string? overrideReason, bool revision, DateOnly madeOn, CancellationToken ct)
    {
        Domain.Entities.Transactions.TransactionType? type = null;
        string? code = null;
        if (transactionTypeId is { } typeId)
        {
            type = await db.TransactionTypes.AsNoTracking().InForce(madeOn).FirstOrDefaultAsync(x => x.Id == typeId, ct);
            if (type is null)
            {
                return Result.Failure<EffectivityDto>("TRANSACTION_TYPE_NOT_IN_FORCE", "The transaction type is not one in force today.");
            }
            code = type.Code;
        }
        else if (revision && faas.Value.GeneralRevisionTransactionCode?.Trim() is { Length: > 0 } revisionCode)
        {
            code = revisionCode;
            type = await db.TransactionTypes.AsNoTracking().InForce(madeOn).FirstOrDefaultAsync(x => x.Code == revisionCode, ct);
        }

        var rule = revision ? EffectivityRule.Fixed : type?.EffectivityRule;
        if (rule is { } needsCause && EffectivityRules.NeedsCauseDate(needsCause))
        {
            if (causeDate is null)
            {
                return Result.Failure<EffectivityDto>("CAUSE_DATE_REQUIRED",
                    $"A {code} reassessment takes effect from the quarter after it is made; enter the date of its cause.");
            }
            if (causeDate > madeOn)
            {
                return Result.Failure<EffectivityDto>("CAUSE_DATE_IN_FUTURE", "The cause of a reassessment cannot be after the date it is made.");
            }
        }

        var derived = rule is { } r ? EffectivityRules.Derive(r, madeOn) : null;
        DateOnly date;
        var overridden = false;
        if (derived is null)
        {
            if (effectiveDate is null)
            {
                return Result.Failure<EffectivityDto>("EFFECTIVE_DATE_REQUIRED",
                    rule is null ? "Enter the effective date, or choose a transaction type whose rule gives it."
                        : $"The {rule} rule does not derive the effective date; enter it.");
            }
            date = effectiveDate.Value;
        }
        else if (effectiveDate is { } given && given != derived)
        {
            // A manual override, only with its reason (audited with the record).
            if (string.IsNullOrWhiteSpace(overrideReason))
            {
                return Result.Failure<EffectivityDto>("EFFECTIVITY_OVERRIDE_REASON_REQUIRED",
                    $"Made today, a {code} assessment takes effect on {derived:yyyy-MM-dd} ({rule}). Give a reason to use {given:yyyy-MM-dd} instead.");
            }
            date = given;
            overridden = true;
        }
        else
        {
            date = derived.Value;
        }

        var window = rule == EffectivityRule.NextQuarter ? type?.CauseWindowDays : null;
        var late = rule == EffectivityRule.NextQuarter && causeDate is { } cause && EffectivityRules.CauseWindowExceeded(cause, madeOn, window);
        return Result.Success(new EffectivityDto(date, date.Year, EffectivityRules.Quarter(date), rule, derived is not null, overridden,
            type?.Id, code, type?.EffectivityLegalBasis, madeOn, causeDate, window, late));
    }

    private sealed record Calculated(Domain.Entities.Valuation Valuation, List<Domain.Entities.AssessmentLine> Lines, EffectivityDto Effectivity);

    /// <summary>
    /// The single assessment calculation (CLAUDE.md Rule 9), shared by create and
    /// preview: the valuation's rows grouped by (classification, actual use), each
    /// assessed at the level in force for its bracket value.
    /// </summary>
    private async Task<Result<Calculated>> CalculateAsync(CreateAssessmentRequest request, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure<Calculated>("VALIDATION_FAILED", string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)));
        }

        var valuation = await db.Valuations.Include(x => x.Lines).FirstOrDefaultAsync(x => x.Id == request.ValuationId, cancellationToken);
        if (valuation is null)
        {
            return Result.Failure<Calculated>("VALUATION_NOT_FOUND", "No Valuation was found with the given id.");
        }
        var resolved = await ResolveEffectivityAsync(request.TransactionTypeId, request.CauseDate, request.EffectiveDate,
            request.EffectivityOverrideReason, request.RevisionReference is not null, clock.Today, cancellationToken);
        if (resolved.IsFailure)
        {
            return Result.Failure<Calculated>(resolved.Code!, resolved.Message!);
        }
        var effectivity = resolved.Value;
        var effectiveDate = effectivity.EffectiveDate;
        // A period is assessed on the value under the rules in force then (docs/analysis/valuation-foundation.md §4.1, Q1).
        if (valuation.EffectiveDate != effectiveDate)
        {
            return Result.Failure<Calculated>("VALUATION_DATE_MISMATCH",
                $"The valuation was made as of {valuation.EffectiveDate:yyyy-MM-dd}, but the assessment takes effect on {effectiveDate:yyyy-MM-dd}. "
                + "Value the unit as of the assessment's effective date, then assess that valuation.");
        }
        // A building's depreciation depends on the transaction it was valued for (valuation-foundation.md §4.5, Q10).
        if (request.RevisionReference is null && valuation.TransactionTypeId != effectivity.TransactionTypeId
            && valuation.Lines.Any(l => l.BreakdownJson.Contains("\"DepreciationPercent\"")))
        {
            return Result.Failure<Calculated>("VALUATION_TRANSACTION_MISMATCH",
                "The building was valued for a different transaction, and the transaction decides whether it takes a new depreciation. "
                + "Value it again for this transaction, then assess that valuation.");
        }

        if (request.PreviousAssessmentId is { } previousId)
        {
            var previous = await db.Assessments.AsNoTracking().Where(x => x.Id == previousId)
                .Select(x => new { x.RpuId, x.Status }).FirstOrDefaultAsync(cancellationToken);
            if (previous is null)
            {
                return Result.Failure<Calculated>("PREVIOUS_ASSESSMENT_NOT_FOUND", "The specified previous assessment does not exist.");
            }
            // The history an assessment continues is the same unit's posted one (docs/analysis/value-and-assess.md §2.4).
            if (previous.RpuId != valuation.RpuId || previous.Status != WorkflowStatus.Posted)
            {
                return Result.Failure<Calculated>("PREVIOUS_ASSESSMENT_INVALID", "The previous assessment must be a posted assessment of the same unit.");
            }
        }

        var lines = valuation.Lines.OrderBy(x => x.Sequence).Select(l => new AssessableLine(l.ClassificationId, l.ActualUseId, l.MarketValue)).ToList();
        var assessed = await AssessLinesAsync(valuation.RpuId, valuation.SourceType, lines, effectiveDate, cancellationToken);
        return assessed.IsFailure
            ? Result.Failure<Calculated>(assessed.Code!, assessed.Message!)
            : Result.Success(new Calculated(valuation, assessed.Value, effectivity));
    }

    /// <summary>
    /// The single assessment of valuation rows (CLAUDE.md Rule 9), for a stored valuation and for one computed
    /// only (a simulation under a proposed SMV, smv-preparation-general-revision.md §4.3): the rows grouped by
    /// (classification, actual use), each group at the level in force on <paramref name="effectiveDate"/> for its
    /// bracket value, then marked taxable or exempt.
    /// </summary>
    public async Task<Result<List<Domain.Entities.AssessmentLine>>> AssessLinesAsync(Guid rpuId, ValuationSourceType sourceType,
        IReadOnlyList<AssessableLine> lines, DateOnly effectiveDate, CancellationToken cancellationToken = default)
    {
        if (lines.Count == 0)
        {
            return Result.Failure<List<Domain.Entities.AssessmentLine>>("VALUATION_HAS_NO_LINES", "The valuation has no appraisal lines to assess.");
        }
        // A line without its own classification or actual use takes the unit's Tax Declaration's.
        Domain.Entities.TaxDeclaration? taxDeclaration = null;
        if (lines.Any(l => l.ClassificationId is null || l.ActualUseId is null))
        {
            taxDeclaration = await TaxDeclarationLookup.GetCurrentAsync(db, rpuId, cancellationToken);
            if (taxDeclaration is null)
            {
                return Result.Failure<List<Domain.Entities.AssessmentLine>>("TAX_DECLARATION_NOT_FOUND",
                    "A Tax Declaration (for its classification/actual use) is required before this can be assessed.");
            }
        }

        var propertyTypeCode = sourceType switch
        {
            ValuationSourceType.Land => PropertyTypeCodes.Land,
            ValuationSourceType.Building => PropertyTypeCodes.Building,
            ValuationSourceType.Machinery => PropertyTypeCodes.Machinery,
            _ => throw new InvalidOperationException($"Unhandled {nameof(ValuationSourceType)}: {sourceType}"),
        };
        var propertyType = await db.PropertyTypes.FirstOrDefaultAsync(x => x.Code == propertyTypeCode, cancellationToken);
        if (propertyType is null)
        {
            return Result.Failure<List<Domain.Entities.AssessmentLine>>("PROPERTY_TYPE_NOT_CONFIGURED", $"No PropertyType with code '{propertyTypeCode}' is configured.");
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
            var level = await ResolveAssessmentLevelAsync(group.Key.Classification, group.Key.ActualUse, propertyType.Id, bracketValue, effectiveDate, cancellationToken);
            if (level is null)
            {
                var names = await db.Classifications.Where(x => x.Id == group.Key.Classification).Select(x => x.Name).FirstOrDefaultAsync(cancellationToken)
                    + " / " + await db.ActualUses.Where(x => x.Id == group.Key.ActualUse).Select(x => x.Name).FirstOrDefaultAsync(cancellationToken);
                return Result.Failure<List<Domain.Entities.AssessmentLine>>("ASSESSMENT_LEVEL_NOT_FOUND",
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
                AssessedValue = Money.ToCentavo(marketValue * level.AssessmentPercentage / 100m),
            });
        }
        // Taxable or exempt by the exemptions in force on the effective date (assessment-listing-exemptions.md §4.1).
        await ExemptionTaxability.MarkAsync(db, rpuId, assessmentLines, effectiveDate, cancellationToken);
        return Result.Success(assessmentLines);
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
        // An assessment is made when it is finally approved (Q3). If approving it today would give its rule another
        // effectivity (a December draft approved in January), it must be valued and assessed again as of that date.
        var today = clock.Today;
        if (assessment.EffectivityRule is { } rule && assessment.EffectivityOverrideReason is null
            && EffectivityRules.Derive(rule, today) is { } derivedToday && derivedToday != assessment.EffectiveDate)
        {
            return Result.Failure<AssessmentDto>("EFFECTIVITY_CHANGED",
                $"Approved today, this assessment would take effect on {derivedToday:yyyy-MM-dd}, not {assessment.EffectiveDate:yyyy-MM-dd}. "
                + $"Reject it, then value and assess the unit again as of {derivedToday:yyyy-MM-dd}.");
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
            if (MakerChecker.Refusal(currentUser, assessment.CreatedBy, "CANNOT_APPROVE_OWN_ASSESSMENT",
                    "The assessment's creator cannot also approve it.") is { } refusal)
            {
                return Result.Failure<AssessmentDto>(refusal.Code, refusal.Message);
            }
        }
        if (!step.Value.ChainInForce || step.Value.Completed)
        {
            assessment.Status = WorkflowStatus.Approved;
            assessment.ApprovedBy = currentUser.AppUserId;
            assessment.ApprovedAt = DateTimeOffset.UtcNow;
            assessment.MadeOn = today;
            // The lines are marked as of when the assessment is made: an exemption decided since the draft counts (§4.1).
            await ExemptionTaxability.MarkAsync(db, assessment.RpuId,
                await db.AssessmentLines.Where(l => l.AssessmentId == assessment.Id).ToListAsync(cancellationToken), assessment.EffectiveDate, cancellationToken);
            if (assessment.CauseDate is { } cause && assessment.EffectivityRule == EffectivityRule.NextQuarter)
            {
                assessment.CauseWindowExceeded = EffectivityRules.CauseWindowExceeded(cause, today, assessment.CauseWindowDays);
            }
            // With FAAS numbers of their own, number the appraisal record once approved, if a FAAS
            // scheme is in force. By default the FAAS number is the TD's (docs/analysis/mrpaao-forms-model.md §6.1).
            if (faas.Value.NumberSource == FaasNumberSource.Own)
            {
                var context = await NumberContexts.ForPropertyAsync(db, assessment.PropertyId, assessment.AssessmentYear, cancellationToken, clock.Today);
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
        catch (DbUpdateException ex) when (ex is not DbUpdateConcurrencyException) // a row-version conflict is a 409 CONCURRENCY_CONFLICT
        {
            // IX_ApprovalRecords_SubjectType_SubjectId_StepSequence: someone signed this step at the same time.
            return Result.Failure<AssessmentDto>("APPROVAL_STEP_CONFLICT", "This approval step was signed by someone else at the same time. Reload and try again.");
        }

        return Result.Success(await MapAsync(assessment.Id, cancellationToken));
    }

    public async Task<Result<AssessmentDto>> RejectAsync(Guid assessmentId, string reason, CancellationToken cancellationToken = default)
    {
        reason = reason?.Trim() ?? string.Empty;
        if (reason.Length is 0 or > 1000)
        {
            return Result.Failure<AssessmentDto>("VALIDATION_FAILED", "A reason is required (max 1000).");
        }
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
        // Back-tax periods are posted in order, so each period's FAAS/TD cancels the one before (valuation-foundation.md §4.8, Q15).
        if (await db.BackTaxPeriods.Where(p => p.AssessmentId == assessment.Id).Select(p => new { p.BackTaxRunId, p.Sequence }).FirstOrDefaultAsync(cancellationToken)
                is { } period
            && await db.BackTaxPeriods.Where(p => p.BackTaxRunId == period.BackTaxRunId && p.Sequence < period.Sequence && p.Assessment!.Status != WorkflowStatus.Posted)
                .OrderBy(p => p.Sequence).Select(p => (int?)p.Sequence).FirstOrDefaultAsync(cancellationToken) is { } earlier)
        {
            return Result.Failure<AssessmentDto>("BACK_TAX_PERIOD_ORDER",
                $"Back-tax period {period.Sequence} is posted after the periods before it; period {earlier} is not posted yet.");
        }

        assessment.Status = WorkflowStatus.Posted;
        assessment.PostedAt = clock.UtcNow; // the Record of Assessment entry (MRPAAO Att. 1–3)
        assessment.PostedBy = currentUser.AppUserId;
        var ownsTransaction = db.Database.CurrentTransaction is null;
        await using var transaction = ownsTransaction ? await db.Database.BeginTransactionAsync(cancellationToken) : null;
        string? note = null;
        if (faas.Value.PrepareTdOnPosting)
        {
            // The new FAAS: a Draft TD declaring this assessment, for the assessor to review and approve.
            if (await FaasTaxDeclarations.PrepareForPostedAsync(db, numbering, assessment, clock.Today, faas.Value, cancellationToken) is { } skipped)
            {
                logger.LogInformation("No Tax Declaration prepared for posted assessment {AssessmentId}: {Reason}", assessment.Id, skipped);
                note = $"No Tax Declaration was prepared: {skipped}";
            }
            else
            {
                note = "A draft Tax Declaration declaring this assessment was prepared for review.";
            }
        }
        await db.SaveChangesAsync(cancellationToken);
        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        return Result.Success(await MapAsync(assessment.Id, cancellationToken) with { TaxDeclarationNote = note });
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

    /// <summary>
    /// After an exemption of the unit is approved or ended (assessment-listing-exemptions.md §4.1, Q3): when its
    /// assessment in force would now be marked differently, a Draft reassessment with the same values, the lines
    /// re-marked as of its effectivity and the posted assessment as its previous one. It goes through the normal
    /// workflow; posting it prepares the replacing TD. Posted records never change. The effectivity follows the
    /// transaction type <paramref name="transactionCode"/> when its rule derives a date (the exemption's date as the
    /// cause); otherwise it is <paramref name="from"/>, never before the assessment in force. The caller saves.
    /// Returns the draft, or null with the reason none was opened.
    /// </summary>
    public async Task<(Domain.Entities.Assessment? Draft, string Note)> ReassessTaxabilityAsync(Guid rpuId, DateOnly from, string reason,
        string? transactionCode, CancellationToken cancellationToken = default)
    {
        var ct = cancellationToken;
        var today = clock.Today;
        var latest = await db.Assessments.Include(x => x.Lines)
            .Where(x => x.RpuId == rpuId && (x.Status == WorkflowStatus.Approved || x.Status == WorkflowStatus.Posted))
            .OrderByDescending(x => x.EffectiveDate).ThenByDescending(x => x.CreatedAt).FirstOrDefaultAsync(ct);
        if (latest is null)
        {
            return (null, "The unit has no assessment yet; its first assessment takes the exemption into account.");
        }
        if (latest.Status != WorkflowStatus.Posted)
        {
            return (null, "The unit's latest assessment is approved but not posted; post it, then reassess the unit for the exemption.");
        }
        if (await db.Assessments.AnyAsync(x => x.RpuId == rpuId && (x.Status == WorkflowStatus.Draft || x.Status == WorkflowStatus.PendingReview), ct))
        {
            return (null, "An assessment of the unit is in progress; it takes the exemption into account when it is approved.");
        }

        Domain.Entities.Transactions.TransactionType? type = null;
        if (transactionCode?.Trim() is { Length: > 0 } code)
        {
            type = await db.TransactionTypes.AsNoTracking().InForce(today).FirstOrDefaultAsync(x => x.Code == code, ct);
            if (type is null)
            {
                return (null, $"No transaction type {code} is in force (Exemptions:ReassessmentTransactionCode); reassess the unit by hand.");
            }
        }
        var derived = type?.EffectivityRule is { } rule ? EffectivityRules.Derive(rule, today) : null;
        var effective = derived ?? (from > latest.EffectiveDate ? from : latest.EffectiveDate);

        var lines = latest.Lines.OrderBy(l => l.Sequence).Select(l => new Domain.Entities.AssessmentLine
        {
            Sequence = l.Sequence, ClassificationId = l.ClassificationId, ActualUseId = l.ActualUseId, PropertyTypeId = l.PropertyTypeId,
            MarketValue = l.MarketValue, AssessmentLevelId = l.AssessmentLevelId, AssessmentPercentage = l.AssessmentPercentage, AssessedValue = l.AssessedValue,
            Taxability = l.Taxability, PropertyExemptionId = l.PropertyExemptionId, TaxabilityNote = l.TaxabilityNote,
        }).ToList();
        if (!await ExemptionTaxability.MarkAsync(db, rpuId, lines, effective, ct))
        {
            return (null, $"No reassessment is needed: the assessment in force is already taxable or exempt as the exemptions in force on {effective:yyyy-MM-dd} make it.");
        }
        var draft = new Domain.Entities.Assessment
        {
            RpuId = latest.RpuId, PropertyId = latest.PropertyId, ValuationId = latest.ValuationId, AssessmentYear = effective.Year,
            MarketValue = latest.MarketValue, AssessmentLevelId = latest.AssessmentLevelId, AssessmentPercentage = latest.AssessmentPercentage,
            AssessedValue = latest.AssessedValue, Status = WorkflowStatus.Draft, EffectiveDate = effective,
            TransactionTypeId = type?.Id, TransactionCode = type?.Code, EffectivityRule = derived is null ? null : type?.EffectivityRule,
            CauseDate = derived is not null && EffectivityRules.NeedsCauseDate(type!.EffectivityRule!.Value) ? (from <= today ? from : today) : null,
            PreviousAssessmentId = latest.Id,
            Remarks = $"Taxability reassessment: {reason} Values of the {latest.AssessmentYear} assessment effective {latest.EffectiveDate:yyyy-MM-dd} unchanged.",
            Lines = lines,
        };
        db.Assessments.Add(draft);
        return (draft, $"A draft reassessment effective {effective:yyyy-MM-dd} was opened; approving and posting it prepares the replacing Tax Declaration.");
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
                // EndDate is inclusive: superseding a level ends it the day before its successor (AssessmentLevelService).
                && (x.EndDate == null || x.EndDate >= asOf)
                // "Over the lower, not over the upper" (LGC §218 table form); a lower value of 0 includes 0.
                // DOMAIN VERIFICATION REQUIRED against the LGU's ordinance (docs/analysis/value-and-assess.md §2.6).
                && (x.LowerValue == 0 || x.LowerValue < marketValue)
                && (x.UpperValue == null || marketValue <= x.UpperValue))
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static IQueryable<Domain.Entities.Assessment> WithLines(IQueryable<Domain.Entities.Assessment> query) => query
        .Include(x => x.Lines).ThenInclude(l => l.Classification)
        .Include(x => x.Lines).ThenInclude(l => l.ActualUse)
        .Include(x => x.Lines).ThenInclude(l => l.PropertyExemption).ThenInclude(e => e!.ExemptionType);

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
            l.ActualUseId, l.ActualUse!.Name, l.MarketValue, l.AssessmentLevelId, l.AssessmentPercentage, l.AssessedValue,
            l.Taxability, l.PropertyExemptionId, l.TaxabilityNote, l.PropertyExemption?.ExemptionType?.LegalBasis)).ToList(),
        x.PostedAt,
        x.PostedBy,
        null,
        x.EffectivityYear,
        x.EffectivityQuarter,
        x.TransactionTypeId,
        x.TransactionCode,
        x.EffectivityRule,
        x.CauseDate,
        x.CauseWindowDays,
        x.CauseWindowExceeded,
        x.MadeOn,
        x.EffectivityOverrideReason,
        x.RowVersion);
}
