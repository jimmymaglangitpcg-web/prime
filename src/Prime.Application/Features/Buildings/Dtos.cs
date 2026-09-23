using Prime.Domain.Enums;

namespace Prime.Application.Features.Buildings;

public sealed record CreateBuildingRequest(
    Guid RpuId,
    Guid BuildingTypeId,
    Guid StructuralTypeId,
    Guid ActualUseId,
    int? NumberOfStoreys,
    decimal FloorArea,
    decimal TotalFloorArea,
    int? YearConstructed,
    int? YearCompleted,
    Guid ConditionId,
    decimal? CompletionPercentage);

public sealed record BuildingDto(
    Guid Id,
    Guid RpuId,
    Guid PropertyId,
    Guid BuildingTypeId,
    string BuildingTypeName,
    Guid StructuralTypeId,
    string StructuralTypeName,
    Guid ActualUseId,
    string ActualUseName,
    int NumberOfStoreys,
    decimal FloorArea,
    decimal TotalFloorArea,
    int? YearConstructed,
    int? YearCompleted,
    Guid ConditionId,
    string ConditionName,
    decimal CompletionPercentage,
    decimal? MarketValue,
    decimal? Depreciation,
    decimal? DepreciatedValue,
    decimal? AssessedValue,
    RecordStatus Status,
    DateTimeOffset CreatedAt);
