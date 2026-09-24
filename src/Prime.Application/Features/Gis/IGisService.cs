using Prime.Application.Common;

namespace Prime.Application.Features.Gis;

public interface IGisService
{
    /// <summary>
    /// Active parcels intersecting a WGS84 bounding box given as
    /// "minLon,minLat,maxLon,maxLat" (the OpenLayers/GeoJSON bbox order).
    /// </summary>
    Task<Result<ParcelFeatureCollection>> GetParcelsInExtentAsync(string? bbox, int? limit, CancellationToken cancellationToken = default);

    /// <summary>
    /// Active parcels containing (or touching) a WGS84 point — normally one;
    /// more than one indicates overlapping boundaries, a data-quality issue
    /// the caller should surface rather than hide.
    /// </summary>
    Task<Result<ParcelFeatureCollection>> GetParcelsAtPointAsync(double lon, double lat, CancellationToken cancellationToken = default);
}
