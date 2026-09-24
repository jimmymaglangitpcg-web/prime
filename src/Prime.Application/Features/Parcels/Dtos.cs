using Prime.Domain.Enums;

namespace Prime.Application.Features.Parcels;

public sealed record CreateParcelRequest(
    Guid PropertyId,
    Guid BarangayId,
    Guid? ZoneId,
    /// <summary>WKT (Well-Known Text), e.g. "POLYGON((...))". SRID applied server-side — see docs/DATABASE.md §9.</summary>
    string? GeometryWkt,
    decimal? Area,
    string? SurveyNumber,
    string? LotNumber,
    string? BlockNumber);

public sealed record ParcelDto(
    Guid Id,
    Guid PropertyId,
    Guid BarangayId,
    string BarangayName,
    Guid? ZoneId,
    string? GeometryWkt,
    /// <summary>Declared area in sqm, as entered.</summary>
    decimal? Area,
    /// <summary>Area in sqm measured from the geometry by PostGIS; null when there is no geometry.</summary>
    decimal? MeasuredArea,
    /// <summary>How MeasuredArea was computed, e.g. "GEODESIC_WGS84" or "PROJECTED_EPSG_3123".</summary>
    string? MeasuredAreaBasis,
    string? SurveyNumber,
    string? LotNumber,
    string? BlockNumber,
    RecordStatus Status,
    DateTimeOffset CreatedAt,
    /// <summary>Concurrency token; send back unchanged on updates.</summary>
    uint Version);

/// <summary>
/// Sets or replaces a parcel's boundary. <see cref="Reason"/> is required
/// when a boundary already exists — the previous WKT is preserved in the
/// audit log with this reason (CLAUDE.md §48/§76).
/// </summary>
public sealed record SetParcelGeometryRequest(string GeometryWkt, uint Version, string? Reason);
