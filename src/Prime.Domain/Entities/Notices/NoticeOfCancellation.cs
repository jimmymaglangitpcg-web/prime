using Prime.Domain.Common;
using Prime.Domain.Enums;

namespace Prime.Domain.Entities.Notices;

/// <summary>
/// Notice of Cancellation (LAM 2025 Book III p.89 C; docs/analysis/assessment-listing-exemptions.md §4.4, Q11): tells
/// the previous declarant and the others with a legal interest that a Tax Declaration was cancelled on the assessor's
/// own motion, or that a reassessment cancelled the assessment declared in a previous owner's name. Generated as a
/// Draft when the cancellation is approved, one per addressee address; issued and served like the Notice of Assessment.
/// Everything it states is frozen on it.
/// </summary>
public sealed class NoticeOfCancellation : AuditableEntity
{
    public Guid PropertyId { get; set; }
    public PropertyEntity? Property { get; set; }
    /// <summary>The cancelled declaration.</summary>
    public Guid TaxDeclarationId { get; set; }
    public TaxDeclaration? TaxDeclaration { get; set; }
    public string TaxDeclarationNumber { get; set; } = string.Empty;
    /// <summary>The declaration that replaced it, if any.</summary>
    public Guid? ReplacedByTaxDeclarationId { get; set; }
    public string? ReplacedByTaxDeclarationNumber { get; set; }
    public Guid? PropertyTransactionId { get; set; }

    public CancellationNoticeGround Ground { get; set; }
    /// <summary>Why the declaration was cancelled, as recorded on it.</summary>
    public string Reason { get; set; } = string.Empty;
    public DateOnly CancelledOn { get; set; }

    public string AddresseeNames { get; set; } = string.Empty;
    public string? AddresseeAddress { get; set; }

    public string? NoticeNumber { get; set; }
    public NoticeStatus Status { get; set; } = NoticeStatus.Draft;
    public DateTimeOffset? IssuedAt { get; set; }
    public Guid? IssuedBy { get; set; }

    public NoticeServiceMode? ServiceMode { get; set; }
    public DateOnly? ReceivedDate { get; set; }
    public string? ServedTo { get; set; }
    public string? EmailAddress { get; set; }
    public DateOnly? SentDate { get; set; }
    public string? ProofReference { get; set; }
    public string? ServiceNotes { get; set; }
    public DateTimeOffset? ServiceRecordedAt { get; set; }
    public Guid? ServiceRecordedBy { get; set; }

    public DateTimeOffset? CancelledAt { get; set; }
    public Guid? CancelledBy { get; set; }
    public string? CancellationReason { get; set; }
}
