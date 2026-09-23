using Prime.Domain.Common;
using Prime.Domain.Entities.Reference;
using Prime.Domain.Enums;

namespace Prime.Domain.Entities;

/// <summary>
/// CLAUDE.md §29. Versioned reference data for the assessment percentage
/// applicable to a classification/actual use/property type value bracket.
/// Not yet consumed by any service in Phase 5 — <c>AssessmentService</c>
/// (Phase 6) applies this to a computed <see cref="Valuation.ComputedMarketValue"/>
/// to produce an assessed value; this phase only builds the versioned
/// reference data itself.
///
/// docs/DOMAIN-MODEL.md §3.12 names a field "OrdinanceId", but no
/// <c>Ordinance</c> table exists anywhere in the codebase — <see cref="Smv"/>
/// doesn't reference one either, it stores ordinance fields inline. Resolved
/// the same way here rather than inventing a dangling FK or a new entity
/// out of this phase's scope.
/// </summary>
public sealed class AssessmentLevel : AuditableEntity
{
    public string OrdinanceNumber { get; set; } = string.Empty;
    public DateOnly? OrdinanceDate { get; set; }

    public Guid ClassificationId { get; set; }
    public Classification? Classification { get; set; }
    public Guid ActualUseId { get; set; }
    public ActualUse? ActualUse { get; set; }
    public Guid PropertyTypeId { get; set; }
    public PropertyType? PropertyType { get; set; }

    public decimal LowerValue { get; set; }
    public decimal? UpperValue { get; set; }
    public decimal AssessmentPercentage { get; set; }

    public DateOnly EffectiveDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public WorkflowStatus Status { get; set; } = WorkflowStatus.Draft;

    public Guid? ApprovedBy { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
}
