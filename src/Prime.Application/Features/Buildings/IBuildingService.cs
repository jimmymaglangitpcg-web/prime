using Prime.Application.Common;

namespace Prime.Application.Features.Buildings;

public interface IBuildingService
{
    Task<Result<BuildingDto>> CreateAsync(CreateBuildingRequest request, CancellationToken cancellationToken = default);
    Task<Result<BuildingDto>> GetByIdAsync(Guid buildingId, CancellationToken cancellationToken = default);
    Task<Result<BuildingDto>> AddUsePortionAsync(Guid buildingId, AddBuildingUsePortionRequest request, CancellationToken cancellationToken = default);
    Task<Result<BuildingDto>> AddComponentAsync(Guid buildingId, AddBuildingComponentRequest request, CancellationToken cancellationToken = default);
    Task<Result<BuildingDto>> GetByRpuAsync(Guid rpuId, CancellationToken cancellationToken = default);
}
