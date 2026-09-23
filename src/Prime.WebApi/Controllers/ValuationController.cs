using Microsoft.AspNetCore.Mvc;
using Prime.Application.Features.Valuation;

namespace Prime.WebApi.Controllers;

[Route("api/valuation")]
public class ValuationController(IValuationService valuationService) : ApiControllerBase
{
    [HttpPost("land/{landId:guid}")]
    public async Task<ActionResult<ValuationDto>> ComputeForLand(Guid landId, CancellationToken cancellationToken) =>
        HandleCreated(await valuationService.ComputeForLandAsync(landId, cancellationToken), nameof(GetById), dto => new { id = dto.Id });

    [HttpPost("buildings/{buildingId:guid}")]
    public async Task<ActionResult<ValuationDto>> ComputeForBuilding(Guid buildingId, CancellationToken cancellationToken) =>
        HandleCreated(await valuationService.ComputeForBuildingAsync(buildingId, cancellationToken), nameof(GetById), dto => new { id = dto.Id });

    [HttpPost("machinery/{machineryId:guid}")]
    public async Task<ActionResult<ValuationDto>> ComputeForMachinery(Guid machineryId, CancellationToken cancellationToken) =>
        HandleCreated(await valuationService.ComputeForMachineryAsync(machineryId, cancellationToken), nameof(GetById), dto => new { id = dto.Id });

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ValuationDto>> GetById(Guid id, CancellationToken cancellationToken) =>
        HandleResult(await valuationService.GetByIdAsync(id, cancellationToken));

    [HttpGet("~/api/rpus/{rpuId:guid}/valuations")]
    public async Task<ActionResult<IReadOnlyList<ValuationDto>>> ListByRpu(Guid rpuId, CancellationToken cancellationToken) =>
        HandleResult(await valuationService.ListByRpuAsync(rpuId, cancellationToken));
}
