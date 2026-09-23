using Microsoft.AspNetCore.Mvc;
using Prime.Application.Features.MachineryUnits;

namespace Prime.WebApi.Controllers;

[Route("api/machinery")]
public class MachineryController(IMachineryService machineryService) : ApiControllerBase
{
    [HttpPost]
    public async Task<ActionResult<MachineryDto>> Create(CreateMachineryRequest request, CancellationToken cancellationToken) =>
        HandleCreated(await machineryService.CreateAsync(request, cancellationToken), nameof(GetById), dto => new { id = dto.Id });

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<MachineryDto>> GetById(Guid id, CancellationToken cancellationToken) =>
        HandleResult(await machineryService.GetByIdAsync(id, cancellationToken));

    [HttpGet("~/api/rpus/{rpuId:guid}/machinery")]
    public async Task<ActionResult<MachineryDto>> GetByRpu(Guid rpuId, CancellationToken cancellationToken) =>
        HandleResult(await machineryService.GetByRpuAsync(rpuId, cancellationToken));
}
