using Prime.Domain.Common;
using Prime.Domain.Enums;

namespace Prime.Domain.Entities.Transactions;

/// <summary>
/// A summons to the owner of a discovered property to declare it (LAM 2025 Book III p.85–86; LGC §213;
/// docs/analysis/assessment-listing-exemptions.md §4.4, Q10): issued on a new-discovery transaction, served like a
/// notice, due a configured number of days after receipt (15), and closed as complied with or not. A second summons
/// follows only an unanswered first; after an unanswered second, the transaction records the verification with other
/// agencies and the assessor declares the property (LGC §204).
/// </summary>
public sealed class DiscoverySummons : AuditableEntity
{
    public Guid PropertyTransactionId { get; set; }
    public PropertyTransaction? PropertyTransaction { get; set; }
    /// <summary>1 or 2.</summary>
    public int Sequence { get; set; }
    public string? SummonsNumber { get; set; }

    public string AddresseeName { get; set; } = string.Empty;
    public string? AddresseeAddress { get; set; }
    public Guid? AddresseeTaxpayerId { get; set; }
    public DateOnly IssuedOn { get; set; }
    /// <summary>Frozen from configuration when issued.</summary>
    public int PeriodDays { get; set; }

    public NoticeServiceMode? ServiceMode { get; set; }
    public DateOnly? ReceivedOn { get; set; }
    public string? ServedTo { get; set; }
    public string? ProofReference { get; set; }
    public string? ServiceNotes { get; set; }
    /// <summary><see cref="ReceivedOn"/> + <see cref="PeriodDays"/>.</summary>
    public DateOnly? DueDate { get; set; }

    public SummonsOutcome Outcome { get; set; } = SummonsOutcome.Pending;
    public DateOnly? OutcomeOn { get; set; }
    public string? OutcomeNotes { get; set; }
}
