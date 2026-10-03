using Prime.Domain.Common;
using Prime.Domain.Entities.Reference;

namespace Prime.Domain.Entities;

/// <summary>
/// The highest assessment level the law allows for a kind of property, optionally a classification or actual use, in a
/// value bracket (LGC §218; docs/analysis/assessment-listing-exemptions.md §4.2, Q6). Configuration, versioned by
/// <see cref="Code"/> and approved by a second user; the values are content, never code (CLAUDE.md §7, §118). While
/// one is in force for an assessment level's keys, a level above it cannot be created or approved.
/// </summary>
public sealed class AssessmentLevelCeiling : EffectiveDatedConfiguration
{
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }

    public Guid PropertyTypeId { get; set; }
    public PropertyType? PropertyType { get; set; }
    /// <summary>Null: every classification.</summary>
    public Guid? ClassificationId { get; set; }
    public Classification? Classification { get; set; }
    /// <summary>Null: every actual use.</summary>
    public Guid? ActualUseId { get; set; }
    public ActualUse? ActualUse { get; set; }

    /// <summary>The bracket, read as the levels are: over the lower, not over the upper (null: unbounded).</summary>
    public decimal LowerValue { get; set; }
    public decimal? UpperValue { get; set; }

    public decimal MaximumPercentage { get; set; }
}
