using Microsoft.AspNetCore.Mvc;
using Prime.Application.Features.RealPropertyUnits;
using Prime.Application.Common.Security;
using Prime.WebApi.Authorization;

namespace Prime.WebApi.Controllers;

public class RpusController(IRealPropertyUnitService rpuService) : ApiControllerBase
{
    [RequirePermission(Permissions.PropertyEdit)]
    [HttpPost]
    public async Task<ActionResult<RpuDto>> Create(CreateRpuRequest request, CancellationToken cancellationToken) =>
        HandleCreated(await rpuService.CreateAsync(request, cancellationToken), nameof(GetById), dto => new { id = dto.Id });

    [RequirePermission(Permissions.PropertyView)]
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<RpuDto>> GetById(Guid id, CancellationToken cancellationToken) =>
        HandleResult(await rpuService.GetByIdAsync(id, cancellationToken));

    [RequirePermission(Permissions.PropertyView)]
    [HttpGet("~/api/properties/{propertyId:guid}/rpus")]
    public async Task<ActionResult<IReadOnlyList<RpuDto>>> ListByProperty(Guid propertyId, CancellationToken cancellationToken) =>
        HandleResult(await rpuService.ListByPropertyAsync(propertyId, cancellationToken));
}
