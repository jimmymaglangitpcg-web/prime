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
    decimal? ReplacementCost = null,
    Guid? ClassificationId = null,
    Guid? ActualUseId = null,
    bool IsImported = false,
    string? AcquisitionCurrency = null,
    decimal? ForeignAcquisitionCost = null,
    string? OriginCountry = null,
    string? PriceIndexSeries = null,
    DateOnly? DateInstalled = null,
    bool IsInOperation = true,
    IReadOnlyList<MachineryCostItemRequest>? CostItems = null);

/// <summary>One acquisition cost item: freight, insurance, duties, installation … (LAM Bk III p.75).</summary>
public sealed record MachineryCostItemRequest(MachineryCostItemKind Kind, decimal Amount, string? Description);

public sealed record MachineryCostItemDto(int Sequence, MachineryCostItemKind Kind, decimal Amount, string? Description);

/// <summary>
/// What the derived replacement cost reads (valuation-foundation.md §4.6), changed with a reason
/// (audited); the cost items given replace the machine's list.
/// </summary>
public sealed record UpdateMachineryValuationInputsRequest(
    bool IsImported, string? AcquisitionCurrency, decimal? ForeignAcquisitionCost, string? OriginCountry, string? PriceIndexSeries,
    DateOnly? DateInstalled, bool IsInOperation, IReadOnlyList<MachineryCostItemRequest> CostItems, string Reason);

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
    DateTimeOffset CreatedAt,
    Guid? ClassificationId,
    string? ClassificationName,
    Guid? ActualUseId,
    string? ActualUseName,
    int? YearInstalled = null,
    int? YearOfInitialOperation = null,
    decimal? ConversionFactor = null,
    bool IsImported = false,
    string? AcquisitionCurrency = null,
    decimal? ForeignAcquisitionCost = null,
    string? OriginCountry = null,
    string? PriceIndexSeries = null,
    DateOnly? DateInstalled = null,
    bool IsInOperation = true,
    IReadOnlyList<MachineryCostItemDto>? CostItems = null);
