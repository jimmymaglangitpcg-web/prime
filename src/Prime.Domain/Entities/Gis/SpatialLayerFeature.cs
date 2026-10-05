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
/// Tax map section boundary version (MRPAAO Ch. II §2; docs/analysis/property-identification.md
/// §3.7): the extent of one tax map sheet. One current row per section.
/// </summary>
public sealed class SectionBoundary : SpatialLayerFeature
{
    public Guid SectionId { get; set; }
    public TaxMapSection? Section { get; set; }
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

/// <summary>
/// An area in dispute between LGUs or barangays (LAM Bk II pp.50–52), drawn hatched on tax-map sheets with the PINs
/// of the parcels it touches. Effective-dated like the other reference layers; keyed by its <see cref="Code"/>.
/// </summary>
public sealed class DisputedArea : SpatialLayerFeature
{
    public string Code { get; set; } = string.Empty;
    public string? Name { get; set; }
    public MultiPolygon Geometry { get; set; } = null!;
}

/// <summary>
/// A sub-market area drawn or imported for the land value map (LAM 2025 Book IV p.110; docs/analysis/smv-preparation-general-revision.md
/// §4.4, Q11): an area a sub-class covers, shown where there are no parcels yet. Keyed by its own code; the name says what it is
/// (e.g. the sub-class and place). Shown only; never used to value a property.
/// </summary>
public sealed class SubMarketArea : SpatialLayerFeature
{
    public string Code { get; set; } = string.Empty;
    public string? Name { get; set; }
    public MultiPolygon Geometry { get; set; } = null!;
}
