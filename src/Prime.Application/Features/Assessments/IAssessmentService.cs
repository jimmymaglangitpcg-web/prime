using Prime.Application.Common;

namespace Prime.Application.Features.Assessments;

public interface IAssessmentService
{
    Task<Result<AssessmentDto>> CreateAsync(CreateAssessmentRequest request, CancellationToken cancellationToken = default);
    Task<Result<AssessmentPreviewDto>> PreviewAsync(CreateAssessmentRequest request, CancellationToken cancellationToken = default);
    /// <summary>The effectivity an assessment with these inputs would take if made today (docs/analysis/valuation-foundation.md §4.2).</summary>
    Task<Result<EffectivityDto>> EffectivityAsync(Guid? transactionTypeId, DateOnly? causeDate, DateOnly? effectiveDate, string? overrideReason,
        CancellationToken cancellationToken = default);
    Task<Result<AssessmentDto>> SubmitForReviewAsync(Guid assessmentId, CancellationToken cancellationToken = default);
    Task<Result<AssessmentDto>> ApproveAsync(Guid assessmentId, CancellationToken cancellationToken = default);
    Task<Result<AssessmentDto>> RejectAsync(Guid assessmentId, string reason, CancellationToken cancellationToken = default);
    Task<Result<AssessmentDto>> PostAsync(Guid assessmentId, CancellationToken cancellationToken = default);
    Task<Result<AssessmentDto>> GetByIdAsync(Guid assessmentId, CancellationToken cancellationToken = default);
    /// <summary>A Draft reassessment re-marking the unit's taxability after an exemption decision (assessment-listing-exemptions.md Q3); the caller saves.</summary>
    Task<(Domain.Entities.Assessment? Draft, string Note)> ReassessTaxabilityAsync(Guid rpuId, DateOnly from, string reason, string? transactionCode,
        CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<AssessmentDto>>> ListByRpuAsync(Guid rpuId, DateOnly? asOfDate, CancellationToken cancellationToken = default);
    /// <summary>
    /// The assessment rows of valuation rows, stored or not (smv-preparation-general-revision.md §4.3): grouped by
    /// classification and use, at the levels in force on <paramref name="effectiveDate"/>, marked taxable or exempt.
    /// Nothing is saved.
    /// </summary>
    Task<Result<List<Domain.Entities.AssessmentLine>>> AssessLinesAsync(Guid rpuId, Domain.Enums.ValuationSourceType sourceType,
        IReadOnlyList<AssessableLine> lines, DateOnly effectiveDate, CancellationToken cancellationToken = default);
}

/// <summary>One valuation row to assess; a null classification or use is the unit's Tax Declaration's.</summary>
public sealed record AssessableLine(Guid? ClassificationId, Guid? ActualUseId, decimal MarketValue);
