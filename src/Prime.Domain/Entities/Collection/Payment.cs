using Prime.Domain.Common;
using Prime.Domain.Entities.Billing;
using Prime.Domain.Entities.Reference;
using Prime.Domain.Enums;

namespace Prime.Domain.Entities.Collection;

/// <summary>
/// A collection: money received against posted bills, receipted once
/// (docs/analysis/collection.md §3; CLAUDE.md §40). Carries the DOF DO
/// 054-2024 §7.1 eOR minimum content: office and location code, payor, date
/// AND time, the amounts by nature of collection coded to revenue accounts
/// (<see cref="Allocations"/>), the OR number, a separate transaction number,
/// the mode of payment (<see cref="Tenders"/>) and, through each allocation's
/// bill, the bill/TD number. Everything printed is frozen here.
///
/// Payments are never edited or deleted: a void, reversal or correction
/// changes <see cref="Status"/> and leaves the record (CLAUDE.md §49, §76).
/// </summary>
public sealed class Payment : AuditableEntity
{
    /// <summary>System-generated, unique; distinct from the OR number (eOR §7.1).</summary>
    public string TransactionNumber { get; set; } = string.Empty;
    /// <summary>Unique across every status: a voided receipt's number is never reused.</summary>
    public string OfficialReceiptNumber { get; set; } = string.Empty;
    /// <summary>Sent by the client once per intended payment; a repeat returns the first payment (CLAUDE.md §66).</summary>
    public string IdempotencyKey { get; set; } = string.Empty;

    public Guid? PayorTaxpayerId { get; set; }
    public Taxpayer? PayorTaxpayer { get; set; }
    public string PayorName { get; set; } = string.Empty;
    public string? PayorAddress { get; set; }

    /// <summary>The LGU-local date of payment; the date charges are computed on.</summary>
    public DateOnly PaymentDate { get; set; }
    /// <summary>When the payment was received (date and time, eOR §7.1).</summary>
    public DateTimeOffset ReceivedAt { get; set; }
    /// <summary>Frozen from <c>Lgu:Office</c>.</summary>
    public string? Office { get; set; }
    /// <summary>Frozen from <c>Lgu:LocationCode</c>.</summary>
    public string? LocationCode { get; set; }
    public Guid? CashierUserId { get; set; }

    /// <summary>Σ allocations.</summary>
    public decimal AmountDue { get; set; }
    /// <summary>Σ tenders.</summary>
    public decimal AmountTendered { get; set; }
    /// <summary>Tendered − due; only from tenders whose mode allows change.</summary>
    public decimal Change { get; set; }

    public PaymentStatus Status { get; set; } = PaymentStatus.Posted;
    /// <summary>When the approved void or reversal took effect.</summary>
    public DateTimeOffset? CancelledAt { get; set; }
    /// <summary>For a correction's replacement: the payment it corrects (voided or reversed in the same step).</summary>
    public Guid? ReplacesPaymentId { get; set; }
    public string? Remarks { get; set; }

    public List<PaymentTender> Tenders { get; set; } = [];
    public List<PaymentAllocation> Allocations { get; set; } = [];
    public List<PaymentCancellation> Cancellations { get; set; } = [];
}

/// <summary>
/// A request to void or reverse a payment, or to correct it (void/reverse and
/// reissue), and its maker-checker decision (docs/analysis/collection.md §4.4–§4.5;
/// CLAUDE.md §46 "payment reversal"). The requester is <see cref="AuditableEntity"/>
/// CreatedBy and can never decide it. Whether it is a void or a reversal is
/// fixed on approval (<see cref="Kind"/>). An approved cancellation carries its
/// own transaction number (DOF DO 054-2024 §7.1: a transaction number also for
/// eOR cancellation). Requests are never deleted; a rejected one stays on record.
/// </summary>
public sealed class PaymentCancellation : AuditableEntity
{
    public Guid PaymentId { get; set; }
    public Payment? Payment { get; set; }
    public string Reason { get; set; } = string.Empty;
    /// <summary>A correction: the replacement payment is posted when this is approved.</summary>
    public bool IsCorrection { get; set; }
    /// <summary>The replacement as requested (JSON), for a correction.</summary>
    public string? ReplacementRequestJson { get; set; }

    public PaymentCancellationStatus Status { get; set; } = PaymentCancellationStatus.Pending;
    public PaymentCancellationKind? Kind { get; set; }
    public Guid? DecidedBy { get; set; }
    public DateTimeOffset? DecidedAt { get; set; }
    public string? DecisionRemarks { get; set; }
    public string? TransactionNumber { get; set; }
    public Guid? ReplacementPaymentId { get; set; }
}

/// <summary>One mode of payment within a payment (cash, a check, a transfer …).</summary>
public sealed class PaymentTender : Entity
{
    public Guid PaymentId { get; set; }
    public Guid PaymentModeId { get; set; }
    public PaymentMode? PaymentMode { get; set; }
    public decimal Amount { get; set; }
    public string? Reference { get; set; }
    public string? Bank { get; set; }
    public DateOnly? CheckDate { get; set; }
}

/// <summary>
/// One allocation line (from <c>CollectionCalculator</c>): principal, discount,
/// penalty or interest for one installment of one tax type, with its rule, the
/// frozen rate and the revenue account it is coded to. Principal lines count
/// against what is owed for as long as the payment stands.
/// </summary>
public sealed class PaymentAllocation : Entity
{
    public Guid PaymentId { get; set; }
    public int LineNumber { get; set; }
    public Guid TaxBillId { get; set; }
    public TaxBill? TaxBill { get; set; }
    public Guid PropertyId { get; set; }
    public Guid RpuId { get; set; }
    public int TaxYear { get; set; }
    public int InstallmentSequence { get; set; }
    public DateOnly DueDate { get; set; }
    public Guid TaxTypeId { get; set; }
    public TaxType? TaxType { get; set; }
    public BillingComponent Component { get; set; }
    public Guid RuleId { get; set; }
    public decimal? RatePercent { get; set; }
    public decimal BaseAmount { get; set; }
    public decimal Amount { get; set; }
    public int? Months { get; set; }
    public CollectionYearCategory YearCategory { get; set; }
    public string Explanation { get; set; } = string.Empty;

    public Guid RevenueAccountMappingId { get; set; }
    public string AccountCode { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public string? Fund { get; set; }
}

/// <summary>
/// Modes of payment the LGU accepts (docs/analysis/collection.md §3). The list
/// is LGU configuration; development data is DEMO only.
/// </summary>
public sealed class PaymentMode : LookupEntity
{
    /// <summary>A reference (check number, transfer reference) is required.</summary>
    public bool RequiresReference { get; set; }
    /// <summary>Change may be given from this mode (cash).</summary>
    public bool AllowsChange { get; set; }
}

/// <summary>
/// Which revenue account a collection line is coded to — the eOR "amount
/// detailed by nature of collection, coded to subsidiary-ledger revenue
/// classification" (DOF DO 054-2024 §7.1). Scope: (TaxTypeId, Component,
/// YearCategory). The chart of accounts is the LGU's/COA's: DOMAIN
/// VERIFICATION REQUIRED; PRIME supplies none.
/// </summary>
public sealed class RevenueAccountMapping : EffectiveDatedConfiguration
{
    public Guid TaxTypeId { get; set; }
    public TaxType? TaxType { get; set; }
    public BillingComponent Component { get; set; }
    public CollectionYearCategory YearCategory { get; set; }
    public string AccountCode { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public string? Fund { get; set; }
}
