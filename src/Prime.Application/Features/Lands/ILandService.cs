using Prime.Application.Common;

namespace Prime.Application.Features.Lands;

public interface ILandService
{
    Task<Result<LandDto>> CreateAsync(CreateLandRequest request, CancellationToken cancellationToken = default);
    Task<Result<LandDto>> GetByIdAsync(Guid landId, CancellationToken cancellationToken = default);
    Task<Result<LandDto>> GetByRpuAsync(Guid rpuId, CancellationToken cancellationToken = default);
}
