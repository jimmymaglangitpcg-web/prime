using Prime.Application.Common;

namespace Prime.Application.Features.Lands;

public interface ILandService
{
    Task<Result<LandDto>> CreateAsync(CreateLandRequest request, CancellationToken cancellationToken = default);
    Task<Result<LandDto>> GetByIdAsync(Guid landId, CancellationToken cancellationToken = default);
    Task<Result<LandDto>> GetByRpuAsync(Guid rpuId, CancellationToken cancellationToken = default);
    Task<Result<LandDto>> AddStripAsync(Guid landId, AddLandStripRequest request, CancellationToken cancellationToken = default);
    Task<Result<LandDto>> AddImprovementAsync(Guid landId, AddLandImprovementRequest request, CancellationToken cancellationToken = default);
    Task<Result<LandDto>> AddAdjustmentAsync(Guid landId, AddLandAdjustmentRequest request, CancellationToken cancellationToken = default);
}
