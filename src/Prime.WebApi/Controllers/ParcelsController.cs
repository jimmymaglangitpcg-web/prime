using Microsoft.AspNetCore.Mvc;
using Prime.Application.Features.Parcels;

namespace Prime.WebApi.Controllers;

public class ParcelsController(IParcelService parcelService) : ApiControllerBase
{
    [HttpPost]
    public async Task<ActionResult<ParcelDto>> Create(CreateParcelRequest request, CancellationToken cancellationToken) =>
        HandleCreated(await parcelService.CreateAsync(request, cancellationToken), nameof(GetById), dto => new { id = dto.Id });

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ParcelDto>> GetById(Guid id, CancellationToken cancellationToken) =>
        HandleResult(await parcelService.GetByIdAsync(id, cancellationToken));

    [HttpPut("{id:guid}/geometry")]
    public async Task<ActionResult<ParcelDto>> SetGeometry(Guid id, SetParcelGeometryRequest request, CancellationToken cancellationToken) =>
        HandleResult(await parcelService.SetGeometryAsync(id, request, cancellationToken));

    [HttpGet("~/api/properties/{propertyId:guid}/parcels")]
    public async Task<ActionResult<IReadOnlyList<ParcelDto>>> ListByProperty(Guid propertyId, CancellationToken cancellationToken) =>
        HandleResult(await parcelService.ListByPropertyAsync(propertyId, cancellationToken));
}
