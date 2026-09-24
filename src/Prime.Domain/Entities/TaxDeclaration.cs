using Prime.Domain.Common;
using Prime.Domain.Entities.Reference;
using Prime.Domain.Enums;

namespace Prime.Domain.Entities;

/// <summary>
/// CLAUDE.md §23. Never physically delete historical Tax Declarations —
/// superseded by a new row via <see cref="PreviousTaxDeclarationId"/>, not
/// overwritten. <see cref="Status"/> is the maker-checker
/// <see cref="WorkflowStatus"/> (CLAUDE.md §45), not an LGU-configurable
/// classification.
///
/// Lifecycle (docs/FORMS-REVISION-PLAN.md §5 A4): Draft → PendingReview →
/// Approved (maker-checker, or the configured approval chain) → Cancelled.
/// Approving a TD that names a <see cref="PreviousTaxDeclarationId"/>
/// cancels that one ("this declaration cancels TD No. …"), recording
/// <see cref="SupersededByTaxDeclarationId"/> on it. At most one Approved TD
/// per RPU.
/// </summary>
public sealed class TaxDeclaration : AuditableEntity
{
    public Guid RpuId { get; set; }
    public RealPropertyUnit? Rpu { get; set; }
    public Guid PropertyId { get; set; }
    public PropertyEntity? Property { get; set; }

    public string TaxDeclarationNumber { get; set; } = string.Empty;
    public int RevisionNumber { get; set; } = 1;
    public DateOnly EffectivityDate { get; set; }
    public Taxability Taxability { get; set; } = Taxability.Taxable;

    public Guid ClassificationId { get; set; }
    public Classification? Classification { get; set; }
    public Guid ActualUseId { get; set; }
    public ActualUse? ActualUse { get; set; }
    public Guid? SubClassificationId { get; set; }
    public SubClassification? SubClassification { get; set; }

    public int AssessmentYear { get; set; }
    public WorkflowStatus Status { get; set; } = WorkflowStatus.Draft;

    public Guid? PreviousTaxDeclarationId { get; set; }
    public TaxDeclaration? PreviousTaxDeclaration { get; set; }

    public string? Remarks { get; set; }
    public Guid? ApprovedBy { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }

    /// <summary>
    /// The transaction this TD was drafted under, if any. Such a TD is approved
    /// only through its transaction (docs/FORMS-REVISION-PLAN.md A5).
    /// </summary>
    public Guid? PropertyTransactionId { get; set; }

    public DateTimeOffset? CancelledAt { get; set; }
    public Guid? CancelledBy { get; set; }
    public string? CancellationReason { get; set; }
    /// <summary>The TD that cancelled this one, when it was superseded rather than cancelled outright.</summary>
    public Guid? SupersededByTaxDeclarationId { get; set; }

    public List<TaxDeclarationAnnotation> Annotations { get; set; } = [];
}

/// <summary>
/// A note recorded on a Tax Declaration — e.g. a levy (LTOM §150: the levy
/// is annotated on the TD). Kinds are an LGU-configurable lookup
/// (<see cref="AnnotationType"/>), since their list and wording come from the
/// LAM. Annotations are never deleted: lifting one records who, when and why
/// (CLAUDE.md §76).
/// DOMAIN VERIFICATION REQUIRED: whether annotations carry over to a TD that
/// supersedes this one; they are not copied automatically.
/// </summary>
public sealed class TaxDeclarationAnnotation : AuditableEntity
{
    public Guid TaxDeclarationId { get; set; }
    public Guid AnnotationTypeId { get; set; }
    public AnnotationType? AnnotationType { get; set; }
    public string Text { get; set; } = string.Empty;
    /// <summary>E.g. the warrant of levy number.</summary>
    public string? ReferenceNumber { get; set; }
    public DateOnly? ReferenceDate { get; set; }
    public DateOnly EffectiveDate { get; set; }

    public DateTimeOffset? LiftedAt { get; set; }
    public Guid? LiftedBy { get; set; }
    public string? LiftReason { get; set; }
    public string? LiftReference { get; set; }
}
