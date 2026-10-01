using Prime.Application.Common;

namespace Prime.Application.Features.Valuation;

/// <summary>
/// Values a unit as of a date (docs/analysis/valuation-foundation.md §4.1): every rule — SMV,
/// schedules, factors — is the one in force on <c>asOf</c> (default today). An assessment
/// uses a valuation made as of its own effectivity date.
/// </summary>
public interface IValuationService
{
    Task<Result<ValuationDto>> ComputeForLandAsync(Guid landId, CancellationToken cancellationToken = default, DateOnly? asOf = null);
    Task<Result<ValuationDto>> ComputeForBuildingAsync(Guid buildingId, CancellationToken cancellationToken = default, DateOnly? asOf = null);
    Task<Result<ValuationDto>> ComputeForMachineryAsync(Guid machineryId, CancellationToken cancellationToken = default, DateOnly? asOf = null);
    Task<Result<ValuationDto>> ComputeForRpuAsync(Guid rpuId, CancellationToken cancellationToken = default, DateOnly? asOf = null);
    Task<Result<ValuationDto>> GetByIdAsync(Guid valuationId, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<ValuationDto>>> ListByRpuAsync(Guid rpuId, CancellationToken cancellationToken = default);
}
