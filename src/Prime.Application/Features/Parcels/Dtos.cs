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
    decimal? Area,
    string? SurveyNumber,
    string? LotNumber,
    string? BlockNumber,
    RecordStatus Status,
    DateTimeOffset CreatedAt);
