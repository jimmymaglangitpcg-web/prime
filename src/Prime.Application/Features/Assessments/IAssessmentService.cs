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
}
