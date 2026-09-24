namespace Prime.Application.Common.Interfaces;

/// <summary>
/// Measures stored geometry in real-world units. Implemented in
/// Infrastructure because the computation runs in PostGIS (geodesic area or
/// reprojection to a configured projected CRS) — never on raw WGS84 degrees.
/// </summary>
public interface IGeometryMeasurementService
{
    /// <summary>
    /// Describes how areas are measured, e.g. "GEODESIC_WGS84" or
    /// "PROJECTED_EPSG_3123" — returned alongside every measured value so the
    /// figure is reproducible (CLAUDE.md §31/§77).
    /// </summary>
    string AreaBasis { get; }

    /// <summary>Area in square metres for each parcel that has geometry; parcels without geometry are omitted.</summary>
    Task<IReadOnlyDictionary<Guid, decimal>> GetParcelAreasAsync(IReadOnlyCollection<Guid> parcelIds, CancellationToken cancellationToken = default);
}
