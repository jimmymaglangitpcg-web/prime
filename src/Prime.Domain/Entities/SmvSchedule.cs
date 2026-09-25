using Prime.Domain.Common;
using Prime.Domain.Entities.Reference;
using Prime.Domain.Enums;

namespace Prime.Domain.Entities;

/// <summary>
/// CLAUDE.md §28. A single rate row for one SMV revision, scoped to a
/// classification/actual use/property type (and optionally a zone).
/// Effective-dated the same way <see cref="PropertyTaxpayer"/> is —
/// superseding a rate closes the old row's <see cref="EndDate"/> and
/// inserts a new one; nothing is ever edited in place (§28 "never
/// overwrite historical SMVs"). Only <see cref="WorkflowStatus.Approved"/>
/// schedules are eligible for use by the valuation engine.
/// </summary>
public sealed class SmvSchedule : AuditableEntity
{
    public Guid SmvId { get; set; }
    public Smv? Smv { get; set; }

    public Guid ClassificationId { get; set; }
    public Classification? Classification { get; set; }
    public Guid ActualUseId { get; set; }
    public ActualUse? ActualUse { get; set; }
    public Guid PropertyTypeId { get; set; }
    public PropertyType? PropertyType { get; set; }
    public Guid? ZoneId { get; set; }
    public Zone? Zone { get; set; }

    /// <summary>
    /// Set for a land improvement's rate (trees, plants — unit such as "per
    /// tree"); null for land and building rates (docs/analysis/mrpaao-forms-model.md §8.3).
    /// </summary>
    public Guid? ImprovementKindId { get; set; }
    public ImprovementKind? ImprovementKind { get; set; }

    public string Unit { get; set; } = "per sqm";
    public decimal MarketValue { get; set; }
    public decimal? MinimumValue { get; set; }
    public decimal? MaximumValue { get; set; }

    public DateOnly EffectiveDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public WorkflowStatus Status { get; set; } = WorkflowStatus.Draft;

    public Guid? ApprovedBy { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
}
