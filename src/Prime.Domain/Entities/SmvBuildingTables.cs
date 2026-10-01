using Prime.Domain.Common;
using Prime.Domain.Entities.Reference;
using Prime.Domain.Enums;

namespace Prime.Domain.Entities;

/// <summary>
/// The SMV's base unit construction cost (BUCC) of a building per square metre, by structural
/// type, optionally by building kind and classification (LAM Bk III p.72; Bk IV pp.108–109;
/// docs/analysis/valuation-foundation.md §4.5). Every cost is the LGU's data. Versioned per
/// (SMV, structural type, kind, classification) with maker-checker approval.
/// </summary>
public sealed class SmvBuildingCost : EffectiveDatedConfiguration
{
    public Guid SmvId { get; set; }
    public Smv? Smv { get; set; }
    public Guid StructuralTypeId { get; set; }
    public StructuralType? StructuralType { get; set; }
    /// <summary>Null: every building kind.</summary>
    public Guid? BuildingTypeId { get; set; }
    public BuildingType? BuildingType { get; set; }
    /// <summary>Null: every classification.</summary>
    public Guid? ClassificationId { get; set; }
    public Classification? Classification { get; set; }
    public decimal CostPerSquareMetre { get; set; }
}

/// <summary>
/// The SMV's cost of an extra item of a building (fence, gate, mezzanine …) per unit, by the
/// building-component type (LAM Bk III p.72). Versioned per (SMV, component type).
/// </summary>
public sealed class SmvExtraItemCost : EffectiveDatedConfiguration
{
    public Guid SmvId { get; set; }
    public Smv? Smv { get; set; }
    public Guid ComponentTypeId { get; set; }
    public BuildingComponentType? ComponentType { get; set; }
    /// <summary>What a quantity counts, e.g. "sqm", "linear m", "each".</summary>
    public string Unit { get; set; } = string.Empty;
    public decimal UnitCost { get; set; }
}

/// <summary>
/// The SMV's depreciation table for a structural type: age bands with their percent, read as
/// <see cref="Reading"/> says, never below <see cref="MinimumRemainingPercent"/> of the cost
/// (docs/analysis/valuation-foundation.md §4.5, Q8). Versioned per (SMV, structural type).
/// </summary>
public sealed class SmvDepreciationSchedule : EffectiveDatedConfiguration
{
    public Guid SmvId { get; set; }
    public Smv? Smv { get; set; }
    public Guid StructuralTypeId { get; set; }
    public StructuralType? StructuralType { get; set; }
    public DepreciationReading Reading { get; set; }
    /// <summary>The remaining value the SMV sets, as a percent of the cost (depreciation never exceeds 100 minus this).</summary>
    public decimal MinimumRemainingPercent { get; set; }
    public List<SmvDepreciationRow> Rows { get; set; } = [];
}

/// <summary>One age band of a depreciation table: from age (years, inclusive) to age (inclusive; null = no limit).</summary>
public sealed class SmvDepreciationRow : Entity
{
    public Guid SmvDepreciationScheduleId { get; set; }
    public int Sequence { get; set; }
    public int FromAge { get; set; }
    public int? ToAge { get; set; }
    public decimal Percent { get; set; }
}
