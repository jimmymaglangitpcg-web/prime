using Microsoft.AspNetCore.Mvc;
using Prime.Application.Features.MachineryUnits;

namespace Prime.WebApi.Controllers;

[Route("api/machinery")]
public class MachineryController(IMachineryService machineryService) : ApiControllerBase
{
    [HttpPost]
    public async Task<ActionResult<MachineryDto>> Create(CreateMachineryRequest request, CancellationToken cancellationToken) =>
        HandleCreated(await machineryService.CreateAsync(request, cancellationToken), nameof(GetById), dto => new { id = dto.Id });

    /// <summary>What the derived replacement cost reads, changed with a reason (docs/analysis/valuation-foundation.md §4.6).</summary>
    [HttpPut("{id:guid}/valuation-inputs")]
    public async Task<ActionResult<MachineryDto>> UpdateValuationInputs(Guid id, UpdateMachineryValuationInputsRequest request, CancellationToken cancellationToken) =>
        HandleResult(await machineryService.UpdateValuationInputsAsync(id, request, cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<MachineryDto>> GetById(Guid id, CancellationToken cancellationToken) =>
        HandleResult(await machineryService.GetByIdAsync(id, cancellationToken));

    /// <summary>Every machine of a machinery RPU (docs/analysis/mrpaao-forms-model.md §8.3).</summary>
    [HttpGet("~/api/rpus/{rpuId:guid}/machinery-units")]
    public async Task<ActionResult<IReadOnlyList<MachineryDto>>> ListByRpu(Guid rpuId, CancellationToken cancellationToken) =>
        HandleResult(await machineryService.ListByRpuAsync(rpuId, cancellationToken));

    [HttpGet("~/api/rpus/{rpuId:guid}/machinery")]
    public async Task<ActionResult<MachineryDto>> GetByRpu(Guid rpuId, CancellationToken cancellationToken) =>
        HandleResult(await machineryService.GetByRpuAsync(rpuId, cancellationToken));
}
