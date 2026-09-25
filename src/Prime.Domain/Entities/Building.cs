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

    // General description (MRPAAO Att. 2; docs/analysis/mrpaao-forms-model.md §10).
    public string? BuildingPermitNumber { get; set; }
    public DateOnly? BuildingPermitDate { get; set; }
    /// <summary>Condominium Certificate of Title, for a condominium unit.</summary>
    public string? CondominiumCertificateNumber { get; set; }
    public DateOnly? CertificateOfCompletionDate { get; set; }
    public DateOnly? CertificateOfOccupancyDate { get; set; }
    public DateOnly? DateConstructed { get; set; }
    public DateOnly? DateOccupied { get; set; }

    public Guid ConditionId { get; set; }
    public Condition? Condition { get; set; }
    public decimal CompletionPercentage { get; set; } = 100m;

    public decimal? MarketValue { get; set; }
    public decimal? Depreciation { get; set; }
    public decimal? DepreciatedValue { get; set; }
    public decimal? AssessedValue { get; set; }

    public RecordStatus Status { get; set; } = RecordStatus.Active;

    /// <summary>Mixed use: the floor area per classification and actual use. None: one use, the TD's.</summary>
    public List<BuildingUsePortion> UsePortions { get; set; } = [];

    /// <summary>Area per floor (MRPAAO Att. 2 "Area of 1st … flr."); they total the total floor area when given.</summary>
    public List<BuildingFloor> Floors { get; set; } = [];

    /// <summary>The structural materials checklist (MRPAAO Att. 2; p.150–152).</summary>
    public List<BuildingMaterial> Materials { get; set; } = [];

    public ICollection<BuildingComponent> Components { get; set; } = [];
}

/// <summary>
/// The part of a building's floor area under one classification and actual
/// use — a mixed-use building's FAAS assessment rows (MRPAAO Att. 2;
/// docs/analysis/mrpaao-forms-model.md §8.3). The portions total the
/// building's total floor area.
/// </summary>
public sealed class BuildingUsePortion : AuditableEntity
{
    public Guid BuildingId { get; set; }
    public int Sequence { get; set; }
    public Guid ClassificationId { get; set; }
    public Classification? Classification { get; set; }
    public Guid ActualUseId { get; set; }
    public ActualUse? ActualUse { get; set; }
    public decimal FloorArea { get; set; }
}

/// <summary>One floor's area (MRPAAO Att. 2).</summary>
public sealed class BuildingFloor : AuditableEntity
{
    public Guid BuildingId { get; set; }
    public int FloorNumber { get; set; }
    public decimal Area { get; set; }
}

/// <summary>
/// One tick of the structural materials checklist: a material of a structure
/// part, on one floor or all floors — or "Others (specify)" as text.
/// </summary>
public sealed class BuildingMaterial : AuditableEntity
{
    public Guid BuildingId { get; set; }
    public Guid StructuralPartId { get; set; }
    public StructuralPart? StructuralPart { get; set; }
    /// <summary>Null with <see cref="OtherSpecify"/>: a material not in the catalogue.</summary>
    public Guid? StructuralMaterialId { get; set; }
    public StructuralMaterial? StructuralMaterial { get; set; }
    public string? OtherSpecify { get; set; }
    /// <summary>Null: every floor.</summary>
    public int? FloorNumber { get; set; }
}

