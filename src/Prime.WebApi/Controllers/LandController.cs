using Microsoft.AspNetCore.Mvc;
using Prime.Application.Features.Lands;

namespace Prime.WebApi.Controllers;

[Route("api/land")]
public class LandController(ILandService landService) : ApiControllerBase
{
    [HttpPost]
    public async Task<ActionResult<LandDto>> Create(CreateLandRequest request, CancellationToken cancellationToken) =>
        HandleCreated(await landService.CreateAsync(request, cancellationToken), nameof(GetById), dto => new { id = dto.Id });

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<LandDto>> GetById(Guid id, CancellationToken cancellationToken) =>
        HandleResult(await landService.GetByIdAsync(id, cancellationToken));

    [HttpGet("~/api/rpus/{rpuId:guid}/land")]
    public async Task<ActionResult<LandDto>> GetByRpu(Guid rpuId, CancellationToken cancellationToken) =>
        HandleResult(await landService.GetByRpuAsync(rpuId, cancellationToken));
}
