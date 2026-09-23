using Prime.Domain.Common;
using Prime.Domain.Entities.Reference;
using Prime.Domain.Enums;

namespace Prime.Domain.Entities;

/// <summary>CLAUDE.md §25.</summary>
public sealed class Building : AuditableEntity
{
    public Guid RpuId { get; set; }
    public RealPropertyUnit? Rpu { get; set; }
    public Guid PropertyId { get; set; }
    public PropertyEntity? Property { get; set; }

    public Guid BuildingTypeId { get; set; }
    public BuildingType? BuildingType { get; set; }
    public Guid StructuralTypeId { get; set; }
    public StructuralType? StructuralType { get; set; }
    public Guid ActualUseId { get; set; }
    public ActualUse? ActualUse { get; set; }

    public int NumberOfStoreys { get; set; } = 1;
    public decimal FloorArea { get; set; }
    public decimal TotalFloorArea { get; set; }

    public int? YearConstructed { get; set; }
    public int? YearCompleted { get; set; }

    public Guid ConditionId { get; set; }
    public Condition? Condition { get; set; }
    public decimal CompletionPercentage { get; set; } = 100m;

    public decimal? MarketValue { get; set; }
    public decimal? Depreciation { get; set; }
    public decimal? DepreciatedValue { get; set; }
    public decimal? AssessedValue { get; set; }

    public RecordStatus Status { get; set; } = RecordStatus.Active;

    public ICollection<BuildingComponent> Components { get; set; } = [];
}
