using Prime.Domain.Common;
using Prime.Domain.Enums;

namespace Prime.Domain.Entities.Transactions;

/// <summary>
/// A kind of assessment transaction the LGU recognises (CLAUDE.md §34;
/// docs/FORMS-REVISION-PLAN.md §4.7). <see cref="Kind"/> is the statutory
/// event PRIME acts on; <see cref="Code"/>, <see cref="Name"/>,
/// <see cref="Rank"/> and the prerequisite checklist are configuration —
/// the LAM supplies the official codes later. Versioned by <see cref="Code"/>:
/// approving a new version ends the previous one; transactions keep the
/// version they were opened under.
/// </summary>
public sealed class TransactionType : EffectiveDatedConfiguration
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public PropertyTransactionKind Kind { get; set; }
    public int? Rank { get; set; }
    public string? Description { get; set; }
    public List<TransactionTypeRequirement> Requirements { get; set; } = [];
}

/// <summary>One prerequisite of a transaction type, e.g. proof of transfer-tax payment (LGC §135(b)).</summary>
public sealed class TransactionTypeRequirement : Entity
{
    public Guid TransactionTypeId { get; set; }
    public int Sequence { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public bool IsMandatory { get; set; } = true;
    public string? LegalBasis { get; set; }
}

/// <summary>
/// One assessment transaction on a property (CLAUDE.md §34–§37) — the
/// single place where Tax Declarations it contains are approved and the
/// ones it replaces or cancels are cancelled (docs/FORMS-REVISION-PLAN.md
/// §4.7). Draft → PendingReview → Approved (maker-checker or the configured
/// approval chain) | Rejected | Cancelled (withdrawn before approval).
/// Everything it changes points back to it, so "why did this change?" is
/// answerable from the record (CLAUDE.md §77, §111).
/// </summary>
public sealed class PropertyTransaction : AuditableEntity
{
    public Guid TransactionTypeId { get; set; }
    public TransactionType? TransactionType { get; set; }
    /// <summary>Frozen from the type version, so the record keeps its meaning after the catalogue changes.</summary>
    public string TypeCode { get; set; } = string.Empty;
    public string TypeName { get; set; } = string.Empty;
    public PropertyTransactionKind Kind { get; set; }

    /// <summary>From the PropertyTransaction numbering scheme in force, if any.</summary>
    public string? TransactionNumber { get; set; }

    /// <summary>The property the transaction is filed on.</summary>
    public Guid PropertyId { get; set; }
    public PropertyEntity? Property { get; set; }
    public DateOnly EffectiveDate { get; set; }
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// For a transfer of one unit (e.g. a building without its land): the unit
    /// whose parties change. Null: the whole property's parties change.
    /// </summary>
    public Guid? TransferRpuId { get; set; }

    public WorkflowStatus Status { get; set; } = WorkflowStatus.Draft;
    public DateTimeOffset? SubmittedAt { get; set; }
    public Guid? ApprovedBy { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
    public DateTimeOffset? ClosedAt { get; set; }
    public Guid? ClosedBy { get; set; }
    /// <summary>Why it was rejected or withdrawn.</summary>
    public string? CloseReason { get; set; }

    public List<PropertyTransactionRequirement> Requirements { get; set; } = [];
    public List<PropertyTransactionParty> NewParties { get; set; } = [];
    public List<PropertyTransactionTdCancellation> TdCancellations { get; set; } = [];
    public List<PropertyTransactionProperty> RelatedProperties { get; set; } = [];
}

/// <summary>A prerequisite copied from the type when the transaction is opened, then marked satisfied with evidence.</summary>
public sealed class PropertyTransactionRequirement : Entity
{
    public Guid PropertyTransactionId { get; set; }
    public int Sequence { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public bool IsMandatory { get; set; }
    public string? LegalBasis { get; set; }
    public DateTimeOffset? SatisfiedAt { get; set; }
    public Guid? SatisfiedBy { get; set; }
    /// <summary>E.g. an official receipt or certificate number. File attachments come with document storage.</summary>
    public string? EvidenceReference { get; set; }
    public string? Note { get; set; }
}

/// <summary>
/// A party the property passes to (transfer): on approval, the current
/// owners (and any unknown-owner declaration) are ended and these start on
/// the effective date (CLAUDE.md §35 — never overwritten).
/// </summary>
public sealed class PropertyTransactionParty : Entity
{
    public Guid PropertyTransactionId { get; set; }
    public PropertyPartyRole Role { get; set; }
    public Guid? TaxpayerId { get; set; }
    public Taxpayer? Taxpayer { get; set; }
    public Guid? OwnershipTypeId { get; set; }
    public decimal OwnershipPercentage { get; set; }
}

/// <summary>A Tax Declaration the transaction cancels outright on approval (e.g. the source TDs of a consolidation).</summary>
public sealed class PropertyTransactionTdCancellation : Entity
{
    public Guid PropertyTransactionId { get; set; }
    public Guid TaxDeclarationId { get; set; }
    public TaxDeclaration? TaxDeclaration { get; set; }
}

/// <summary>
/// Another property involved — a subdivision's resulting properties or a
/// consolidation's source properties (CLAUDE.md §36–§37). Recorded for
/// traceability; the properties themselves are registered separately.
/// </summary>
public sealed class PropertyTransactionProperty : Entity
{
    public Guid PropertyTransactionId { get; set; }
    public Guid PropertyId { get; set; }
    public PropertyEntity? Property { get; set; }
    public TransactionPropertyRole Role { get; set; }
}
