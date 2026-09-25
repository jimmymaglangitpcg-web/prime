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

    /// <summary>Mixed use (docs/analysis/mrpaao-forms-model.md §8.3): floor area per classification and actual use.</summary>
    [HttpPost("{id:guid}/use-portions")]
    public async Task<ActionResult<BuildingDto>> AddUsePortion(Guid id, AddBuildingUsePortionRequest request, CancellationToken cancellationToken) =>
        HandleResult(await buildingService.AddUsePortionAsync(id, request, cancellationToken));

    [HttpPost("{id:guid}/components")]
    public async Task<ActionResult<BuildingDto>> AddComponent(Guid id, AddBuildingComponentRequest request, CancellationToken cancellationToken) =>
        HandleResult(await buildingService.AddComponentAsync(id, request, cancellationToken));

    [HttpGet("~/api/rpus/{rpuId:guid}/building")]
    public async Task<ActionResult<BuildingDto>> GetByRpu(Guid rpuId, CancellationToken cancellationToken) =>
        HandleResult(await buildingService.GetByRpuAsync(rpuId, cancellationToken));
}
