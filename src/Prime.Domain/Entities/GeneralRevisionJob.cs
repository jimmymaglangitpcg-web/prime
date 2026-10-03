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

    /// <summary>
    /// The programme this run belongs to (docs/analysis/smv-preparation-general-revision.md §4.6); null for the jobs
    /// started before L6-6 with a list of units.
    /// </summary>
    public Guid? GeneralRevisionProgrammeId { get; set; }
    public GeneralRevisionProgramme? GeneralRevisionProgramme { get; set; }
    /// <summary>A programme run: compile, value, or a batch workflow action. Null for the earlier jobs (value).</summary>
    public GeneralRevisionRunMode? Mode { get; set; }

    /// <summary>
    /// The revision's effectivity: every RPU is valued and assessed as of this date, under the
    /// SMV in force then (docs/analysis/valuation-foundation.md §4.1). Null on jobs run before L1-1,
    /// which valued as of their run date.
    /// </summary>
    public DateOnly? EffectiveDate { get; set; }
    public JobExecutionStatus Status { get; set; } = JobExecutionStatus.Queued;

    public int TotalCount { get; set; }
    public int ProcessedCount { get; set; }
    public int FailedCount { get; set; }

    public Guid? StartedBy { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }

    public string? Remarks { get; set; }

    /// <summary>A Reject run: the reason recorded on every assessment it returns.</summary>
    public string? Reason { get; set; }
}
