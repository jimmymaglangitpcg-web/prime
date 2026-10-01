using Microsoft.AspNetCore.Mvc;
using Prime.Application.Features.Valuation;

namespace Prime.WebApi.Controllers;

/// <summary>
/// Valuations; <c>?asOf=yyyy-MM-dd</c> values as of that date, else today (docs/analysis/valuation-foundation.md §4.1);
/// <c>?transactionTypeId=</c> names the transaction a building is valued for, which decides its depreciation (§4.5).
/// </summary>
[Route("api/valuation")]
public class ValuationController(IValuationService valuationService) : ApiControllerBase
{
    [HttpPost("land/{landId:guid}")]
    public async Task<ActionResult<ValuationDto>> ComputeForLand(Guid landId, [FromQuery] DateOnly? asOf, CancellationToken cancellationToken) =>
        HandleCreated(await valuationService.ComputeForLandAsync(landId, cancellationToken, asOf), nameof(GetById), dto => new { id = dto.Id });

    [HttpPost("buildings/{buildingId:guid}")]
    public async Task<ActionResult<ValuationDto>> ComputeForBuilding(Guid buildingId, [FromQuery] DateOnly? asOf, [FromQuery] Guid? transactionTypeId,
        CancellationToken cancellationToken) =>
        HandleCreated(await valuationService.ComputeForBuildingAsync(buildingId, cancellationToken, asOf, transactionTypeId), nameof(GetById), dto => new { id = dto.Id });

    [HttpPost("machinery/{machineryId:guid}")]
    public async Task<ActionResult<ValuationDto>> ComputeForMachinery(Guid machineryId, [FromQuery] DateOnly? asOf, CancellationToken cancellationToken) =>
        HandleCreated(await valuationService.ComputeForMachineryAsync(machineryId, cancellationToken, asOf), nameof(GetById), dto => new { id = dto.Id });

    /// <summary>Values the whole unit: every appraisal row of its land, building or machinery (docs/analysis/value-and-assess.md §2).</summary>
    [HttpPost("~/api/rpus/{rpuId:guid}/valuations")]
    public async Task<ActionResult<ValuationDto>> ComputeForRpu(Guid rpuId, [FromQuery] DateOnly? asOf, [FromQuery] Guid? transactionTypeId,
        CancellationToken cancellationToken) =>
        HandleCreated(await valuationService.ComputeForRpuAsync(rpuId, cancellationToken, asOf, transactionTypeId), nameof(GetById), dto => new { id = dto.Id });

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ValuationDto>> GetById(Guid id, CancellationToken cancellationToken) =>
        HandleResult(await valuationService.GetByIdAsync(id, cancellationToken));

    [HttpGet("~/api/rpus/{rpuId:guid}/valuations")]
    public async Task<ActionResult<IReadOnlyList<ValuationDto>>> ListByRpu(Guid rpuId, CancellationToken cancellationToken) =>
        HandleResult(await valuationService.ListByRpuAsync(rpuId, cancellationToken));
}
