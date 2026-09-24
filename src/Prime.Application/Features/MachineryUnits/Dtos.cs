using Prime.Domain.Enums;

namespace Prime.Application.Features.MachineryUnits;

public sealed record CreateMachineryRequest(
    Guid RpuId,
    Guid MachineryTypeId,
    string? Description,
    string? Brand,
    string? Model,
    string? SerialNumber,
    decimal? Capacity,
    string? CapacityUnit,
    DateOnly? DateAcquired,
    decimal AcquisitionCost,
    decimal? InstallationCost,
    decimal? OtherCost,
    int? EconomicLifeYears,
    int? RemainingLifeYears,
    bool IsBrandNew = false,
    decimal? ReplacementCost = null);

public sealed record MachineryDto(
    Guid Id,
    Guid RpuId,
    Guid PropertyId,
    Guid MachineryTypeId,
    string MachineryTypeName,
    string? Description,
    string? Brand,
    string? Model,
    string? SerialNumber,
    decimal? Capacity,
    string? CapacityUnit,
    DateOnly? DateAcquired,
    decimal AcquisitionCost,
    decimal? InstallationCost,
    decimal? OtherCost,
    bool IsBrandNew,
    decimal? ReplacementCost,
    int? EconomicLifeYears,
    int? RemainingLifeYears,
    decimal? Depreciation,
    decimal? MarketValue,
    decimal? AssessedValue,
    RecordStatus Status,
    DateTimeOffset CreatedAt);
