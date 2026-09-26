using Prime.Application.Common;

namespace Prime.Application.Features.Assessments;

public interface IAssessmentService
{
    Task<Result<AssessmentDto>> CreateAsync(CreateAssessmentRequest request, CancellationToken cancellationToken = default);
    Task<Result<AssessmentPreviewDto>> PreviewAsync(CreateAssessmentRequest request, CancellationToken cancellationToken = default);
    Task<Result<AssessmentDto>> SubmitForReviewAsync(Guid assessmentId, CancellationToken cancellationToken = default);
    Task<Result<AssessmentDto>> ApproveAsync(Guid assessmentId, CancellationToken cancellationToken = default);
    Task<Result<AssessmentDto>> RejectAsync(Guid assessmentId, string reason, CancellationToken cancellationToken = default);
    Task<Result<AssessmentDto>> PostAsync(Guid assessmentId, CancellationToken cancellationToken = default);
    Task<Result<AssessmentDto>> GetByIdAsync(Guid assessmentId, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<AssessmentDto>>> ListByRpuAsync(Guid rpuId, DateOnly? asOfDate, CancellationToken cancellationToken = default);
}
