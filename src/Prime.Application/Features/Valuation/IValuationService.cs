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
    /// <param name="transactionTypeId">The transaction the building is valued for; its type says whether a new depreciation is allowed (§4.5, Q10).</param>
    /// <param name="generalRevision">Valued for a general revision: a new depreciation.</param>
    Task<Result<ValuationDto>> ComputeForBuildingAsync(Guid buildingId, CancellationToken cancellationToken = default, DateOnly? asOf = null,
        Guid? transactionTypeId = null, bool generalRevision = false);
    Task<Result<ValuationDto>> ComputeForMachineryAsync(Guid machineryId, CancellationToken cancellationToken = default, DateOnly? asOf = null);
    Task<Result<ValuationDto>> ComputeForRpuAsync(Guid rpuId, CancellationToken cancellationToken = default, DateOnly? asOf = null,
        Guid? transactionTypeId = null, bool generalRevision = false);
    Task<Result<ValuationDto>> GetByIdAsync(Guid valuationId, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<ValuationDto>>> ListByRpuAsync(Guid rpuId, CancellationToken cancellationToken = default);
}
