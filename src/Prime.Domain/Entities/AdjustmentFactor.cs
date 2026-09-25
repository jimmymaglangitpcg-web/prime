using Prime.Domain.Common;
using Prime.Domain.Entities.Reference;

namespace Prime.Domain.Entities;

/// <summary>
/// A market value adjustment factor of an SMV ordinance — e.g. corner
/// influence, kind of road, distance to the poblacion (MRPAAO Att. 1 and
/// Att. 12 GR Form 1). Every factor and percentage is the LGU's data; none is
/// built in (CLAUDE.md §5, §7). Versioned per (SMV, code) with maker-checker
/// approval, like other effective-dated configuration.
/// </summary>
public sealed class AdjustmentFactor : EffectiveDatedConfiguration
{
    public Guid SmvId { get; set; }
    public Smv? Smv { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    /// <summary>Signed: +5 raises the base market value by 5%, −10 lowers it by 10%.</summary>
    public decimal Percent { get; set; }
    /// <summary>Null: every classification.</summary>
    public Guid? ClassificationId { get; set; }
    public Classification? Classification { get; set; }
    public string? Description { get; set; }
}
