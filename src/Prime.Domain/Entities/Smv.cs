using Prime.Domain.Common;
using Prime.Domain.Enums;

namespace Prime.Domain.Entities;

/// <summary>
/// CLAUDE.md §28. One row per ordinance/revision — never edited once
/// created; a new SMV revision is a new row (docs/DATABASE.md §4, the same
/// "revision header" pattern <see cref="TaxDeclaration"/> uses). Rate data
/// itself lives on <see cref="SmvSchedule"/>, one-to-many from here.
/// </summary>
public sealed class Smv : AuditableEntity
{
    public string OrdinanceNumber { get; set; } = string.Empty;
    public DateOnly OrdinanceDate { get; set; }
    public DateOnly? ApprovalDate { get; set; }
    public DateOnly EffectivityDate { get; set; }
    public int RevisionYear { get; set; }
    public WorkflowStatus Status { get; set; } = WorkflowStatus.Draft;
    public string? Description { get; set; }

    public Guid? ApprovedBy { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
}
