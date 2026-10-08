using Microsoft.AspNetCore.Mvc;
using Prime.Application.Features.Buildings;
using Prime.Application.Common.Security;
using Prime.WebApi.Authorization;

namespace Prime.WebApi.Controllers;

[Route("api/buildings")]
public class BuildingsController(IBuildingService buildingService) : ApiControllerBase
{
    [RequirePermission(Permissions.PropertyEdit)]
    [HttpPost]
    public async Task<ActionResult<BuildingDto>> Create(CreateBuildingRequest request, CancellationToken cancellationToken) =>
        HandleCreated(await buildingService.CreateAsync(request, cancellationToken), nameof(GetById), dto => new { id = dto.Id });

    [RequirePermission(Permissions.PropertyView)]
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<BuildingDto>> GetById(Guid id, CancellationToken cancellationToken) =>
        HandleResult(await buildingService.GetByIdAsync(id, cancellationToken));

    /// <summary>Mixed use (docs/analysis/mrpaao-forms-model.md §8.3): floor area per classification and actual use.</summary>
    [RequirePermission(Permissions.PropertyEdit)]
    [HttpPost("{id:guid}/use-portions")]
    public async Task<ActionResult<BuildingDto>> AddUsePortion(Guid id, AddBuildingUsePortionRequest request, CancellationToken cancellationToken) =>
        HandleResult(await buildingService.AddUsePortionAsync(id, request, cancellationToken));

    [RequirePermission(Permissions.PropertyEdit)]
    [HttpPost("{id:guid}/components")]
    public async Task<ActionResult<BuildingDto>> AddComponent(Guid id, AddBuildingComponentRequest request, CancellationToken cancellationToken) =>
        HandleResult(await buildingService.AddComponentAsync(id, request, cancellationToken));

    [RequirePermission(Permissions.PropertyView)]
    [HttpGet("~/api/rpus/{rpuId:guid}/building")]
    public async Task<ActionResult<BuildingDto>> GetByRpu(Guid rpuId, CancellationToken cancellationToken) =>
        HandleResult(await buildingService.GetByRpuAsync(rpuId, cancellationToken));
}
