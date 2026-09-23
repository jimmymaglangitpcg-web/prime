using Microsoft.AspNetCore.Mvc;
using Prime.Application.Features.Buildings;

namespace Prime.WebApi.Controllers;

[Route("api/buildings")]
public class BuildingsController(IBuildingService buildingService) : ApiControllerBase
{
    [HttpPost]
    public async Task<ActionResult<BuildingDto>> Create(CreateBuildingRequest request, CancellationToken cancellationToken) =>
        HandleCreated(await buildingService.CreateAsync(request, cancellationToken), nameof(GetById), dto => new { id = dto.Id });

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<BuildingDto>> GetById(Guid id, CancellationToken cancellationToken) =>
        HandleResult(await buildingService.GetByIdAsync(id, cancellationToken));

    [HttpGet("~/api/rpus/{rpuId:guid}/building")]
    public async Task<ActionResult<BuildingDto>> GetByRpu(Guid rpuId, CancellationToken cancellationToken) =>
        HandleResult(await buildingService.GetByRpuAsync(rpuId, cancellationToken));
}
