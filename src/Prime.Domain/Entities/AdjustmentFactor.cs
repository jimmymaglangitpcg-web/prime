using Prime.Domain.Common;
using Prime.Domain.Entities.Reference;
using Prime.Domain.Enums;

namespace Prime.Domain.Entities;

/// <summary>
/// A market value adjustment factor of an SMV — e.g. corner influence, kind of
/// road, distance to the poblacion, depth (LAM Bk III pp.76–78; MRPAAO Att. 1).
/// Every factor and percentage is the LGU's data; none is built in (CLAUDE.md
/// §5, §7). Versioned per (SMV, code) with maker-checker approval, like other
/// effective-dated configuration. <see cref="RuleKind"/> says how the
/// percentage is found for a land (docs/analysis/valuation-foundation.md §4.4).
/// </summary>
public sealed class AdjustmentFactor : EffectiveDatedConfiguration
{
    public Guid SmvId { get; set; }
    public Smv? Smv { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public AdjustmentRuleKind RuleKind { get; set; } = AdjustmentRuleKind.Flat;
    /// <summary>
    /// <see cref="AdjustmentRuleKind.Flat"/> and <see cref="AdjustmentRuleKind.Corner"/>: signed,
    /// +5 raises the base market value by 5%, −10 lowers it by 10%. Other kinds use <see cref="Rows"/>.
    /// </summary>
    public decimal Percent { get; set; }
    /// <summary>Null: every classification. A depth factor names the class it applies to (residential).</summary>
    public Guid? ClassificationId { get; set; }
    public Classification? Classification { get; set; }
    /// <summary><see cref="AdjustmentRuleKind.ByDistance"/> only.</summary>
    public DistanceReference? DistanceReference { get; set; }
    /// <summary><see cref="AdjustmentRuleKind.Depth"/> only: the standard depth (m) the bands start beyond; recorded for the FAAS.</summary>
    public decimal? StandardDepth { get; set; }
    public string? Description { get; set; }
    public List<AdjustmentFactorRow> Rows { get; set; } = [];
}

/// <summary>
/// One row of a factor's table: a road type, a distance band (over … not over …, km) or a
/// depth band, with its signed percentage.
/// </summary>
public sealed class AdjustmentFactorRow : Entity
{
    public Guid AdjustmentFactorId { get; set; }
    public int Sequence { get; set; }
    public Guid? RoadTypeId { get; set; }
    public RoadType? RoadType { get; set; }
    /// <summary>Distance band: over this (exclusive); null = from zero, inclusive.</summary>
    public decimal? OverValue { get; set; }
    /// <summary>Distance band: not over this (inclusive); null = no upper limit.</summary>
    public decimal? UpToValue { get; set; }
    public int? DepthBand { get; set; }
    public decimal Percent { get; set; }
}
