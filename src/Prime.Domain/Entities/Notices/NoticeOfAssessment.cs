using Prime.Domain.Common;
using Prime.Domain.Enums;

namespace Prime.Domain.Entities.Notices;

/// <summary>
/// Written notice of an assessment (LGC §223; docs/FORMS-REVISION-PLAN.md
/// A6): required when real property is assessed for the first time or an
/// existing assessment is increased or decreased, to be given within the
/// configured period (§223: thirty days) to the person in whose name the
/// property is declared. The appeal period (§226: sixty days) runs from the
/// date of receipt.
///
/// Draft → Issued → Served, or Cancelled. Everything the notice states —
/// addressees, values, reason and the periods applied — is frozen on it,
/// so it stays what was sent after the records or the configuration change.
/// </summary>
public sealed class NoticeOfAssessment : AuditableEntity
{
    public Guid PropertyId { get; set; }
    public PropertyEntity? Property { get; set; }
    public Guid RpuId { get; set; }
    public Guid AssessmentId { get; set; }
    public Assessment? Assessment { get; set; }
    public Guid? TaxDeclarationId { get; set; }

    public string? NoticeNumber { get; set; }
    public NoticeReason Reason { get; set; }
    public decimal? PreviousAssessedValue { get; set; }
    public decimal AssessedValue { get; set; }
    public decimal MarketValue { get; set; }
    public int AssessmentYear { get; set; }
    public DateOnly AssessmentEffectiveDate { get; set; }

    /// <summary>The declared parties at generation, e.g. "DELA CRUZ, Juan; DELA CRUZ, Maria".</summary>
    public string AddresseeNames { get; set; } = string.Empty;
    public string? AddresseeAddress { get; set; }

    /// <summary>Frozen from configuration: days allowed to give the notice (LGC §223).</summary>
    public int IssuePeriodDays { get; set; }
    /// <summary>The last day to give the notice: assessment approval date + <see cref="IssuePeriodDays"/>.</summary>
    public DateOnly IssueDueDate { get; set; }
    /// <summary>Frozen from configuration: days to appeal after receipt (LGC §226).</summary>
    public int AppealPeriodDays { get; set; }

    public NoticeStatus Status { get; set; } = NoticeStatus.Draft;
    public DateTimeOffset? IssuedAt { get; set; }
    public Guid? IssuedBy { get; set; }

    public NoticeServiceMode? ServiceMode { get; set; }
    /// <summary>The date the addressee received the notice — the appeal period runs from it.</summary>
    public DateOnly? ReceivedDate { get; set; }
    public string? ServedTo { get; set; }
    /// <summary>E.g. the registry return card number or the signed receiving copy's reference.</summary>
    public string? ProofReference { get; set; }
    public string? ServiceNotes { get; set; }
    public DateTimeOffset? ServiceRecordedAt { get; set; }
    public Guid? ServiceRecordedBy { get; set; }
    /// <summary><see cref="ReceivedDate"/> + <see cref="AppealPeriodDays"/>, frozen when service is recorded.</summary>
    public DateOnly? AppealDeadline { get; set; }

    public DateTimeOffset? CancelledAt { get; set; }
    public Guid? CancelledBy { get; set; }
    public string? CancellationReason { get; set; }
}
