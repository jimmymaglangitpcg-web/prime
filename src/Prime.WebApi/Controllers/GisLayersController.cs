using Microsoft.AspNetCore.Mvc;
using Prime.Application.Common;
using Prime.Application.Features.Gis.ReferenceLayers;

namespace Prime.WebApi.Controllers;

/// <summary>Reference map layers — barangay boundaries, valuation zones, roads (docs/GIS.md §3).</summary>
[Route("api/gis/layers")]
public class GisLayersController(IReferenceLayerService layerService) : ApiControllerBase
{
    /// <summary>Features valid on asOf (default today) in a WGS84 bbox, as GeoJSON. layer = barangays | zones | roads.</summary>
    [HttpGet("{layer}")]
    public async Task<ActionResult<ReferenceLayerFeatureCollection>> Get(
        string layer, [FromQuery] string? bbox, [FromQuery] DateOnly? asOf, [FromQuery] int? limit, CancellationToken cancellationToken) =>
        TryParseLayer(layer, out var parsed)
            ? HandleResult(await layerService.GetFeaturesAsync(parsed, bbox, asOf, limit, cancellationToken))
            : HandleResult(UnknownLayer<ReferenceLayerFeatureCollection>(layer));

    /// <summary>
    /// Import a GeoJSON FeatureCollection as new versions. dryRun=true (the
    /// default) validates only. A committing request with any error writes
    /// nothing and returns 422 with the full error report.
    /// </summary>
    [HttpPost("{layer}/import")]
    public async Task<ActionResult<ImportReferenceLayerResult>> Import(
        string layer, ImportReferenceLayerRequest request, [FromQuery] bool dryRun = true, CancellationToken cancellationToken = default)
    {
        if (!TryParseLayer(layer, out var parsed))
        {
            return HandleResult(UnknownLayer<ImportReferenceLayerResult>(layer));
        }

        var result = await layerService.ImportAsync(parsed, request, dryRun, cancellationToken);
        if (result.IsSuccess && !dryRun && !result.Value.Committed)
        {
            return UnprocessableEntity(result.Value);
        }
        return HandleResult(result);
    }

    private static bool TryParseLayer(string value, out ReferenceLayer layer) =>
        Enum.TryParse(value, ignoreCase: true, out layer) && Enum.IsDefined(layer) && !int.TryParse(value, out _);

    private static Result<T> UnknownLayer<T>(string value) =>
        Result.Failure<T>("LAYER_NOT_FOUND", $"Unknown layer \"{value}\". Use barangays, zones or roads.");
}
