using Prime.Domain.Common;
using Prime.Domain.Enums;

namespace Prime.Domain.Entities.Forms;

/// <summary>
/// One version of a printable form (docs/FORMS-REVISION-PLAN.md §4.1), e.g.
/// the Tax Declaration. <see cref="Code"/> identifies the form across
/// versions; approving a version ends the previous one of the same code.
/// The template is data stored per deployment — LAM-derived layouts must not
/// be committed to git (plan §7). A <see cref="FormAuthority.PrimeProvisional"/>
/// version is always rendered with a watermark the template cannot remove.
/// </summary>
public sealed class FormDefinition : EffectiveDatedConfiguration
{
    public string Code { get; set; } = string.Empty;
    public int Version { get; set; }
    public string Title { get; set; } = string.Empty;
    public FormSubjectType SubjectType { get; set; }
    public FormAuthority Authority { get; set; }
    /// <summary>Where the layout comes from, e.g. "LAM Form 2, p. 14". A citation, not the content.</summary>
    public string? SourceReference { get; set; }
    /// <summary>Liquid template producing HTML. Values are HTML-encoded on output; scripts never run where it is shown.</summary>
    public string TemplateBody { get; set; } = string.Empty;
}

/// <summary>
/// A form as issued — immutable (docs/FORMS-REVISION-PLAN.md §4.3). The
/// snapshot holds every value the form showed, and the rendered HTML is kept
/// with its SHA-256 hash, so a reprint or certified true copy shows exactly
/// what was issued whatever changes later (CLAUDE.md §76–§77; LGC
/// §472(b)(9)). A changed record needs a new issue; the old one is
/// cancelled, never edited.
/// </summary>
public sealed class IssuedForm : AuditableEntity
{
    public Guid FormDefinitionId { get; set; }
    public FormDefinition? FormDefinition { get; set; }
    public string FormCode { get; set; } = string.Empty;
    public int FormVersion { get; set; }
    public FormAuthority Authority { get; set; }

    public FormSubjectType SubjectType { get; set; }
    public Guid SubjectId { get; set; }
    /// <summary>The subject's own number (TD number, bill number …), frozen.</summary>
    public string? DocumentNumber { get; set; }

    public string DataSnapshotJson { get; set; } = "{}";
    public string RenderedHtml { get; set; } = string.Empty;
    public string RenderedHtmlSha256 { get; set; } = string.Empty;

    /// <summary>Posted = issued and valid; Cancelled = withdrawn (kept for history).</summary>
    public WorkflowStatus Status { get; set; } = WorkflowStatus.Posted;
    public DateTimeOffset IssuedAt { get; set; }
    public Guid? IssuedBy { get; set; }
    public DateTimeOffset? CancelledAt { get; set; }
    public Guid? CancelledBy { get; set; }
    public string? CancellationReason { get; set; }
}
