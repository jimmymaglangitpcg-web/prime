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
public sealed class TaxDeclaration : AuditableEntity, IVersioned
{
    /// <summary>Row version (xmin): optimistic concurrency, see <see cref="IVersioned"/>.</summary>
    public uint RowVersion { get; set; }

    public Guid RpuId { get; set; }
    public RealPropertyUnit? Rpu { get; set; }
    public Guid PropertyId { get; set; }
    public PropertyEntity? Property { get; set; }

    public string TaxDeclarationNumber { get; set; } = string.Empty;
    /// <summary>
    /// The sequence value the TD number was made from — the LAM's assessment count (Book I p.23) — which the
    /// Notice of Assessment number repeats (p.24; docs/analysis/identification-numbering.md §4.1). Null when the
    /// number was typed or assigned before counts were kept.
    /// </summary>
    public long? AssessmentCount { get; set; }
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

    /// <summary>
    /// The assessment this TD declares. The TD together with it is the FAAS
    /// (docs/analysis/mrpaao-forms-model.md §6.1): a new assessment gets a new
    /// TD, and a transfer gets a new TD naming the same assessment.
    /// </summary>
    public Guid? AssessmentId { get; set; }
    public Assessment? Assessment { get; set; }

    /// <summary>
    /// The FAAS transaction code (MRPAAO p.145: SD, CS, DC, PC, DP, DT, TR, RC,
    /// GR as catalogue data) and its rank; when several transactions give rise
    /// to the FAAS, the highest rank (lowest number) is kept (p.167). Frozen
    /// when the TD is drafted.
    /// </summary>
    public string? TransactionCode { get; set; }
    public int? TransactionRank { get; set; }

    public DateTimeOffset? CancelledAt { get; set; }
    public Guid? CancelledBy { get; set; }
    public string? CancellationReason { get; set; }
    /// <summary>The TD that cancelled this one, when it was superseded rather than cancelled outright.</summary>
    /// <summary>
    /// The cancelled declaration a court order restores or revives (assessment-listing-exemptions.md §4.3): this TD is
    /// the new declaration that names it; the cancelled one stays cancelled. Only within a court-order transaction.
    /// </summary>
    public Guid? RestoresTaxDeclarationId { get; set; }

    public Guid? SupersededByTaxDeclarationId { get; set; }

    public List<TaxDeclarationAnnotation> Annotations { get; set; } = [];

    public List<TaxDeclarationCancellationRequest> CancellationRequests { get; set; } = [];
}

/// <summary>
/// A request to cancel an approved Tax Declaration outright, with no successor (e.g. a duplicate). One user asks, with a
/// reason; a second, known user approves it, which cancels the TD, or rejects it with a reason (CLAUDE.md §46;
/// docs/analysis/workflow-security.md Q19). Requests are kept. A cancellation through a property transaction does not
/// use this: the transaction has its own approval.
/// </summary>
public sealed class TaxDeclarationCancellationRequest : AuditableEntity, IVersioned
{
    /// <summary>Row version (xmin): optimistic concurrency, see <see cref="IVersioned"/>.</summary>
    public uint RowVersion { get; set; }

    public Guid TaxDeclarationId { get; set; }
    public TaxDeclaration? TaxDeclaration { get; set; }
    public string Reason { get; set; } = string.Empty;
    /// <summary>PendingReview, then Approved or Rejected.</summary>
    public WorkflowStatus Status { get; set; } = WorkflowStatus.PendingReview;
    public Guid? DecidedBy { get; set; }
    public DateTimeOffset? DecidedAt { get; set; }
    public string? DecisionReason { get; set; }
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

    /// <summary>The annotation of the replaced TD this one was copied from when its TD was approved (records-and-forms.md §4.4).</summary>
    public Guid? CarriedFromAnnotationId { get; set; }
    public TaxDeclarationAnnotation? CarriedFrom { get; set; }

    public DateTimeOffset? LiftedAt { get; set; }
    public Guid? LiftedBy { get; set; }
    public string? LiftReason { get; set; }
    public string? LiftReference { get; set; }
}
