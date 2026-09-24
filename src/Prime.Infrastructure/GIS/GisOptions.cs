namespace Prime.Infrastructure.GIS;

/// <summary>
/// "Gis" configuration section (docs/GIS.md §2). Per-deployment, since the
/// appropriate projected CRS depends on where the LGU is located.
/// </summary>
public sealed class GisOptions
{
    public const string SectionName = "Gis";

    /// <summary>
    /// Projected CRS used for area/distance measurement, e.g. the PRS92
    /// Philippines zone (EPSG:3121–3125) covering the LGU, so measured areas
    /// match survey grid areas. DOMAIN VERIFICATION REQUIRED per LGU. When
    /// null, areas are measured geodesically on the WGS84 ellipsoid instead —
    /// accurate, but not guaranteed to equal a surveyor's grid area.
    /// </summary>
    public int? MeasurementSrid { get; set; }
}
