using Prime.Domain.Common;
using Prime.Domain.Enums;

namespace Prime.Domain.Entities.Collection;

/// <summary>
/// A cashier's remittance of the day's receipts (docs/analysis/collection.md
/// §4.7, step 9e): the cashier's posted, not yet remitted payments of one
/// collection date, with totals frozen by mode of payment (net of change) and
/// by revenue account. Submitted by the cashier (its <see cref="AuditableEntity"/>
/// CreatedBy), then accepted by someone else — or returned with a reason,
/// which frees its payments for a later remittance. <see cref="Items"/> keep
/// which payments it covered even after a return. A payment in a submitted or
/// accepted remittance can no longer be voided, only reversed. The official
/// Report of Collections and Deposits format is COA's: DOMAIN VERIFICATION REQUIRED.
/// </summary>
public sealed class Remittance : AuditableEntity
{
    /// <summary>From the Remittance numbering scheme in force, if any.</summary>
    public string? RemittanceNumber { get; set; }
    public Guid CashierUserId { get; set; }
    public DateOnly CollectionDate { get; set; }
    public RemittanceStatus Status { get; set; } = RemittanceStatus.Submitted;
    public int PaymentCount { get; set; }
    /// <summary>Σ the payments' amounts; equals Σ <see cref="ModeTotals"/> and Σ <see cref="AccountTotals"/>.</summary>
    public decimal TotalAmount { get; set; }
    public string? Remarks { get; set; }
    public Guid? DecidedBy { get; set; }
    public DateTimeOffset? DecidedAt { get; set; }
    public string? DecisionRemarks { get; set; }

    public List<RemittanceItem> Items { get; set; } = [];
    public List<RemittanceModeTotal> ModeTotals { get; set; } = [];
    public List<RemittanceAccountTotal> AccountTotals { get; set; } = [];
}

public sealed class RemittanceItem : Entity
{
    public Guid RemittanceId { get; set; }
    public Guid PaymentId { get; set; }
    public Payment? Payment { get; set; }
    public decimal Amount { get; set; }
}

/// <summary>Money by mode of payment; change is taken from the modes that allow it.</summary>
public sealed class RemittanceModeTotal : Entity
{
    public Guid RemittanceId { get; set; }
    public Guid PaymentModeId { get; set; }
    public PaymentMode? PaymentMode { get; set; }
    public decimal Amount { get; set; }
}

/// <summary>Money by revenue account and fund, from the payments' allocation lines.</summary>
public sealed class RemittanceAccountTotal : Entity
{
    public Guid RemittanceId { get; set; }
    public string AccountCode { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public string? Fund { get; set; }
    public decimal Amount { get; set; }
}
