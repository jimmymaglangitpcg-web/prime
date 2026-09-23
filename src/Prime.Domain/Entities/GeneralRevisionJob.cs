using Prime.Domain.Common;
using Prime.Domain.Enums;

namespace Prime.Domain.Entities;

/// <summary>
/// CLAUDE.md §33/§72: tracks a General Revision batch run as a background
/// job — progress-monitorable, never run inline in a browser request.
/// Purpose-built for this need rather than the generic
/// <c>PropertyTransaction</c> (CLAUDE.md §34, still unbuilt) — General
/// Revision is the only Phase 6 transaction type, so a dedicated small
/// entity is enough; Transfer/Subdivision/Consolidation etc. remain out of
/// scope until a phase that actually needs them. Assessments produced by a
/// run are linked back via <see cref="Assessment.RevisionReference"/>.
/// </summary>
public sealed class GeneralRevisionJob : AuditableEntity
{
    public int RevisionYear { get; set; }
    public JobExecutionStatus Status { get; set; } = JobExecutionStatus.Queued;

    public int TotalCount { get; set; }
    public int ProcessedCount { get; set; }
    public int FailedCount { get; set; }

    public Guid? StartedBy { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }

    public string? Remarks { get; set; }
}
