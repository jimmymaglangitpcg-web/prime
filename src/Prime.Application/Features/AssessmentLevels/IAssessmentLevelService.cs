using Prime.Application.Common;

namespace Prime.Application.Features.AssessmentLevels;

public interface IAssessmentLevelService
{
    Task<Result<AssessmentLevelDto>> CreateAsync(CreateAssessmentLevelRequest request, CancellationToken cancellationToken = default);
    Task<Result<AssessmentLevelDto>> ApproveAsync(Guid assessmentLevelId, CancellationToken cancellationToken = default);
    Task<Result<AssessmentLevelDto>> GetByIdAsync(Guid assessmentLevelId, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<AssessmentLevelDto>>> ListAsync(CancellationToken cancellationToken = default);
}
