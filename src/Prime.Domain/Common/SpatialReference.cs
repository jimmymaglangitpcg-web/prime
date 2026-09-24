namespace Prime.Domain.Common;

/// <summary>
/// Canonical spatial reference for stored geometry (docs/DATABASE.md §9,
/// docs/GIS.md). Decided at the start of Phase 7: store in WGS84 so the web
/// map and interchange formats need no reprojection; area/distance are never
/// computed in these (degree) units — see <c>GisOptions.MeasurementSrid</c>.
/// Survey data supplied in PRS92 (EPSG:4683 / 3121–3125) is reprojected to
/// this SRID on import rather than stored as-is.
/// </summary>
public static class SpatialReference
{
    public const int StorageSrid = 4326;
}
