using Prime.Application.Common;

namespace Prime.Application.Features.GeneralRevision;

public interface IGeneralRevisionService
{
    Task<Result<GeneralRevisionJobDto>> StartAsync(StartGeneralRevisionRequest request, CancellationToken cancellationToken = default);
    Task<Result<GeneralRevisionJobDto>> GetStatusAsync(Guid jobId, CancellationToken cancellationToken = default);
}
