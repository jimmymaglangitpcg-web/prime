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
    DateTimeOffset CreatedAt,
    IReadOnlyList<BuildingUsePortionDto> UsePortions,
    IReadOnlyList<BuildingComponentDto> Components,
    string? BuildingPermitNumber = null,
    DateOnly? BuildingPermitDate = null,
    string? CondominiumCertificateNumber = null,
    DateOnly? CertificateOfCompletionDate = null,
    DateOnly? CertificateOfOccupancyDate = null,
    DateOnly? DateConstructed = null,
    DateOnly? DateOccupied = null,
    IReadOnlyList<BuildingFloorDto>? Floors = null,
    IReadOnlyList<BuildingMaterialDto>? Materials = null);

/// <summary>A mixed-use building's floor area under one classification and use (docs/analysis/mrpaao-forms-model.md §8.3).</summary>
public sealed record AddBuildingUsePortionRequest(Guid ClassificationId, Guid ActualUseId, decimal FloorArea);

public sealed record BuildingUsePortionDto(Guid Id, int Sequence, Guid ClassificationId, string ClassificationName, Guid ActualUseId, string ActualUseName, decimal FloorArea);

/// <summary>
/// A structural component, or an additional item (fence, garage …) whose cost is
/// added to the construction cost. Cost: given, or quantity × unit cost.
/// </summary>
public sealed record AddBuildingComponentRequest(
    Guid ComponentTypeId, string? Description, decimal? Quantity, decimal? UnitCost, decimal? Cost, bool IsAdditionalItem, Guid? BuildingUsePortionId);

public sealed record BuildingComponentDto(
    Guid Id, Guid ComponentTypeId, string ComponentTypeName, string? Description, decimal? Quantity, decimal? UnitCost, decimal? Cost,
    bool IsAdditionalItem, Guid? BuildingUsePortionId);

public sealed record BuildingFloorDto(Guid Id, int FloorNumber, decimal Area);

/// <summary>One tick of the structural materials checklist; <see cref="MaterialName"/> is the catalogue name or the "Others" text.</summary>
public sealed record BuildingMaterialDto(Guid Id, Guid StructuralPartId, string StructuralPartName, Guid? StructuralMaterialId, string MaterialName, int? FloorNumber);

