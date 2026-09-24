using NetTopologySuite.Geometries;
using Prime.Domain.Common;
using Prime.Domain.Entities.Reference;

namespace Prime.Domain.Entities.Gis;

/// <summary>
/// One effective-dated version of a reference-layer shape (docs/GIS.md §3).
/// Boundaries change — barangays are created/merged, valuation zones are
/// redrawn with each SMV revision — so a new shape never overwrites the old
/// one: importing a new version closes the current row (sets
/// <see cref="EndDate"/>) and inserts a new one. The validity interval is
/// half-open: [EffectiveDate, EndDate). Geometry is in
/// <see cref="SpatialReference.StorageSrid"/>.
/// </summary>
public abstract class SpatialLayerFeature : AuditableEntity
{
    public DateOnly EffectiveDate { get; set; }
    public DateOnly? EndDate { get; set; }

    /// <summary>Where the shape came from, e.g. "PSA/NAMRIA 2023 barangay boundaries". Required — provenance of official map data.</summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>Optional document/dataset reference (ordinance no., survey plan, dataset version).</summary>
    public string? SourceReference { get; set; }

    /// <summary>Groups every row written by one import so a batch can be traced as a unit.</summary>
    public Guid ImportBatchId { get; set; }
}

/// <summary>Barangay boundary polygon version. One current (EndDate null) row per barangay.</summary>
public sealed class BarangayBoundary : SpatialLayerFeature
{
    public Guid BarangayId { get; set; }
    public Barangay? Barangay { get; set; }
    public MultiPolygon Geometry { get; set; } = null!;
}

/// <summary>
/// Valuation-zone boundary polygon version (the zones SMV rates refer to).
/// One current row per zone.
/// </summary>
public sealed class ZoneBoundary : SpatialLayerFeature
{
    public Guid ZoneId { get; set; }
    public Zone? Zone { get; set; }
    public MultiPolygon Geometry { get; set; } = null!;
}

/// <summary>
/// Road centreline version, keyed by <see cref="Code"/> — the source
/// dataset's own road identifier. One current row per code.
/// </summary>
public sealed class RoadSegment : SpatialLayerFeature
{
    public string Code { get; set; } = string.Empty;
    public string? Name { get; set; }
    public Guid? RoadTypeId { get; set; }
    public RoadType? RoadType { get; set; }
    public MultiLineString Geometry { get; set; } = null!;
}
