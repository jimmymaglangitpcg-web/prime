using System.Text.Json;

namespace Prime.Application.Features.Gis;

/// <summary>
/// A GeoJSON FeatureCollection (RFC 7946) of parcels. <see cref="Truncated"/>
/// and <see cref="Limit"/> are foreign members (RFC 7946 §6.1): when the
/// requested extent holds more parcels than the limit, the client should
/// zoom in rather than assume it has everything.
/// </summary>
public sealed record ParcelFeatureCollection(
    string Type,
    IReadOnlyList<ParcelFeature> Features,
    bool Truncated,
    int Limit);

/// <summary>
/// One parcel as a GeoJSON Feature. <see cref="Geometry"/> is a GeoJSON
/// geometry object in WGS84 (EPSG:4326, RFC 7946's only CRS).
/// </summary>
public sealed record ParcelFeature(
    string Type,
    Guid Id,
    JsonElement Geometry,
    ParcelFeatureProperties Properties);

/// <summary>
/// Deliberately limited to identifiers and location — no owner names, TINs
/// or values. The map layer is a bulk listing, so personal data stays behind
/// the individually-authorized Property Profile (CLAUDE.md §68).
/// </summary>
public sealed record ParcelFeatureProperties(
    Guid ParcelId,
    Guid PropertyId,
    string PropertyIdentificationNumber,
    string? LotNumber,
    string? BlockNumber,
    string? SurveyNumber,
    string BarangayName);
