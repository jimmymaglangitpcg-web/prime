using Prime.Application.Common;

namespace Prime.Application.Features.RealPropertyUnits;

public interface IRealPropertyUnitService
{
    Task<Result<RpuDto>> CreateAsync(CreateRpuRequest request, CancellationToken cancellationToken = default);
    Task<Result<RpuDto>> GetByIdAsync(Guid rpuId, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<RpuDto>>> ListByPropertyAsync(Guid propertyId, CancellationToken cancellationToken = default);
}
