using Prime.Domain.Enums;

namespace Prime.Domain.Common;

/// <summary>
/// Common shape of configuration that changes over time by approved
/// versions (docs/FORMS-REVISION-PLAN.md §4): numbering schemes, form
/// definitions and approval chains. Same lifecycle as billing rules:
/// Draft → Approved by a second user (CLAUDE.md §46); approving a version
/// ends its predecessor; approved versions are never edited or deleted.
/// <see cref="EndDate"/> is the INCLUSIVE last day.
/// </summary>
public abstract class EffectiveDatedConfiguration : AuditableEntity
{
    /// <summary>Why this configuration exists, e.g. "PRIME provisional" or "LAM (DOF DC 004-2025) Form __". Required — CLAUDE.md §6.</summary>
    public string LegalBasis { get; set; } = string.Empty;
    public DateOnly EffectiveDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public WorkflowStatus Status { get; set; } = WorkflowStatus.Draft;
    public Guid? ApprovedBy { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
    public string? Remarks { get; set; }
}
