using Microsoft.AspNetCore.Mvc;
using Prime.Application.Features.Gis;

namespace Prime.WebApi.Controllers;

public class GisController(IGisService gisService) : ApiControllerBase
{
    /// <summary>Active parcels in a WGS84 extent, as GeoJSON. bbox = "minLon,minLat,maxLon,maxLat".</summary>
    [HttpGet("parcels")]
    public async Task<ActionResult<ParcelFeatureCollection>> ParcelsInExtent([FromQuery] string? bbox, [FromQuery] int? limit, CancellationToken cancellationToken) =>
        HandleResult(await gisService.GetParcelsInExtentAsync(bbox, limit, cancellationToken));

    /// <summary>Active parcels at a WGS84 point (map click), as GeoJSON.</summary>
    [HttpGet("parcels/at")]
    public async Task<ActionResult<ParcelFeatureCollection>> ParcelsAtPoint([FromQuery] double lon, [FromQuery] double lat, CancellationToken cancellationToken) =>
        HandleResult(await gisService.GetParcelsAtPointAsync(lon, lat, cancellationToken));
}
