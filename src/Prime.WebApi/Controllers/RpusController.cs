using Microsoft.AspNetCore.Mvc;
using Prime.Application.Features.RealPropertyUnits;

namespace Prime.WebApi.Controllers;

public class RpusController(IRealPropertyUnitService rpuService) : ApiControllerBase
{
    [HttpPost]
    public async Task<ActionResult<RpuDto>> Create(CreateRpuRequest request, CancellationToken cancellationToken) =>
        HandleCreated(await rpuService.CreateAsync(request, cancellationToken), nameof(GetById), dto => new { id = dto.Id });

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<RpuDto>> GetById(Guid id, CancellationToken cancellationToken) =>
        HandleResult(await rpuService.GetByIdAsync(id, cancellationToken));

    [HttpGet("~/api/properties/{propertyId:guid}/rpus")]
    public async Task<ActionResult<IReadOnlyList<RpuDto>>> ListByProperty(Guid propertyId, CancellationToken cancellationToken) =>
        HandleResult(await rpuService.ListByPropertyAsync(propertyId, cancellationToken));
}
