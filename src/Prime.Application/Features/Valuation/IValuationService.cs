using Prime.Application.Common;

namespace Prime.Application.Features.Valuation;

/// <summary>
/// Values a unit as of a date (docs/analysis/valuation-foundation.md §4.1): every rule — SMV,
/// schedules, factors — is the one in force on <c>asOf</c> (default today). An assessment
/// uses a valuation made as of its own effectivity date. <c>mode</c> prices under a proposed
/// SMV and/or computes without storing (<see cref="ValuationMode"/>); the default stores.
/// </summary>
public interface IValuationService
{
    Task<Result<ValuationDto>> ComputeForLandAsync(Guid landId, CancellationToken cancellationToken = default, DateOnly? asOf = null,
        ValuationMode? mode = null);
    /// <param name="transactionTypeId">The transaction the building is valued for; its type says whether a new depreciation is allowed (§4.5, Q10).</param>
    /// <param name="generalRevision">Valued for a general revision: a new depreciation.</param>
    /// <param name="rulesAsOf">Take the SMV, costs and tables in force on this date instead (back taxes, §4.8); age still counts to <paramref name="asOf"/>.</param>
    Task<Result<ValuationDto>> ComputeForBuildingAsync(Guid buildingId, CancellationToken cancellationToken = default, DateOnly? asOf = null,
        Guid? transactionTypeId = null, bool generalRevision = false, DateOnly? rulesAsOf = null, ValuationMode? mode = null);
    Task<Result<ValuationDto>> ComputeForMachineryAsync(Guid machineryId, CancellationToken cancellationToken = default, DateOnly? asOf = null,
        DateOnly? rulesAsOf = null, ValuationMode? mode = null);
    /// <param name="buildingRulesAsOf">For a building unit: the date whose rules apply (back taxes at the current construction cost, Q14).</param>
    /// <param name="machineryRulesAsOf">For a machinery unit: likewise.</param>
    Task<Result<ValuationDto>> ComputeForRpuAsync(Guid rpuId, CancellationToken cancellationToken = default, DateOnly? asOf = null,
        Guid? transactionTypeId = null, bool generalRevision = false, DateOnly? buildingRulesAsOf = null, DateOnly? machineryRulesAsOf = null,
        ValuationMode? mode = null);
    /// <summary>The unit value land of these keys would take on <paramref name="asOf"/> under the rate source; null when no row applies.</summary>
    Task<SmvRateDto?> LandRateAsync(Guid classificationId, Guid? subClassificationId, Guid? actualUseId, Guid municipalityId, Guid? barangayId,
        DateOnly asOf, ValuationMode mode, CancellationToken cancellationToken = default, Guid? zoneId = null);
    Task<Result<ValuationDto>> GetByIdAsync(Guid valuationId, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<ValuationDto>>> ListByRpuAsync(Guid rpuId, CancellationToken cancellationToken = default);
}
