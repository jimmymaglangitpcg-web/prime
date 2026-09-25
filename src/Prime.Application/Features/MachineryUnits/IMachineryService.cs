using Prime.Application.Common;

namespace Prime.Application.Features.MachineryUnits;

public interface IMachineryService
{
    Task<Result<MachineryDto>> CreateAsync(CreateMachineryRequest request, CancellationToken cancellationToken = default);
    Task<Result<MachineryDto>> GetByIdAsync(Guid machineryId, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<MachineryDto>>> ListByRpuAsync(Guid rpuId, CancellationToken cancellationToken = default);
    Task<Result<MachineryDto>> GetByRpuAsync(Guid rpuId, CancellationToken cancellationToken = default);
}
