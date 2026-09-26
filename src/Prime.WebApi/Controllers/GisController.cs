using Microsoft.AspNetCore.Mvc;
using Prime.Application.Features.Gis;

namespace Prime.WebApi.Controllers;

public class GisController(IGisService gisService, ITaxMapSheetService sheets) : ApiControllerBase
{
    /// <summary>Active parcels in a WGS84 extent, as GeoJSON. bbox = "minLon,minLat,maxLon,maxLat".</summary>
    [HttpGet("parcels")]
    public async Task<ActionResult<ParcelFeatureCollection>> ParcelsInExtent([FromQuery] string? bbox, [FromQuery] int? limit, CancellationToken cancellationToken) =>
        HandleResult(await gisService.GetParcelsInExtentAsync(bbox, limit, cancellationToken));

    /// <summary>Active parcels at a WGS84 point (map click), as GeoJSON.</summary>
    [HttpGet("parcels/at")]
    public async Task<ActionResult<ParcelFeatureCollection>> ParcelsAtPoint([FromQuery] double lon, [FromQuery] double lat, CancellationToken cancellationToken) =>
        HandleResult(await gisService.GetParcelsAtPointAsync(lon, lat, cancellationToken));

    /// <summary>A tax map sheet: the section's boundary and heading (MRPAAO Ch. II §2 C.8).</summary>
    [HttpGet("sheets/tax-map/{sectionId:guid}")]
    public async Task<ActionResult<TaxMapSheetDto>> TaxMapSheet(Guid sectionId, [FromQuery] DateOnly? asOf, CancellationToken cancellationToken) =>
        HandleResult(await sheets.TaxMapAsync(sectionId, asOf, cancellationToken));

    /// <summary>A barangay's section index map (MRPAAO Ch. II §2 C.4(a)).</summary>
    [HttpGet("sheets/section-index/{barangayId:guid}")]
    public async Task<ActionResult<TaxMapSheetDto>> SectionIndexSheet(Guid barangayId, [FromQuery] DateOnly? asOf, CancellationToken cancellationToken) =>
        HandleResult(await sheets.SectionIndexAsync(barangayId, asOf, cancellationToken));

    /// <summary>A municipality's or city district's barangay index map (MRPAAO Ch. II §2 C.4(b)).</summary>
    [HttpGet("sheets/barangay-index/{municipalityId:guid}")]
    public async Task<ActionResult<TaxMapSheetDto>> BarangayIndexSheet(Guid municipalityId, [FromQuery] Guid? districtId, [FromQuery] DateOnly? asOf, CancellationToken cancellationToken) =>
        HandleResult(await sheets.BarangayIndexAsync(municipalityId, districtId, asOf, cancellationToken));
}
