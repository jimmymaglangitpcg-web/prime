using Prime.Application.Common;

namespace Prime.Application.Features.Gis.ReferenceLayers;

public interface IReferenceLayerService
{
    /// <summary>
    /// Validates a GeoJSON import and, unless <paramref name="dryRun"/>,
    /// commits it all-or-nothing as new effective-dated versions.
    /// </summary>
    Task<Result<ImportReferenceLayerResult>> ImportAsync(ReferenceLayer layer, ImportReferenceLayerRequest request, bool dryRun, CancellationToken cancellationToken = default);

    /// <summary>Layer features valid on <paramref name="asOf"/> (default: today) within a WGS84 bbox.</summary>
    Task<Result<ReferenceLayerFeatureCollection>> GetFeaturesAsync(ReferenceLayer layer, string? bbox, DateOnly? asOf, int? limit, CancellationToken cancellationToken = default);
}
