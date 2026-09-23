using Prime.Domain.Enums;

namespace Prime.Application.Features.Lands;

public sealed record CreateLandRequest(
    Guid RpuId,
    decimal Area,
    string? AreaUnit,
    Guid ClassificationId,
    Guid ActualUseId,
    Guid? SubClassificationId,
    Guid? ZoneId,
    decimal? LocationFactor,
    decimal? RoadFrontage,
    Guid? RoadTypeId,
    bool IsCornerLot,
    string? Zoning);

public sealed record LandDto(
    Guid Id,
    Guid RpuId,
    Guid PropertyId,
    decimal Area,
    string AreaUnit,
    Guid ClassificationId,
    string ClassificationName,
    Guid ActualUseId,
    string ActualUseName,
    Guid? SubClassificationId,
    Guid? ZoneId,
    decimal? LocationFactor,
    decimal? RoadFrontage,
    Guid? RoadTypeId,
    bool IsCornerLot,
    string? Zoning,
    decimal? MarketValue,
    decimal? AssessedValue,
    RecordStatus Status,
    DateTimeOffset CreatedAt);
