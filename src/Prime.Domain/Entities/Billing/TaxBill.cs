using Prime.Domain.Common;
using Prime.Domain.Entities.Reference;
using Prime.Domain.Enums;

namespace Prime.Domain.Entities.Billing;

/// <summary>
/// A Real Property Tax bill for one RPU and tax year, computed as of a date
/// (docs/BILLING.md §4; CLAUDE.md §39). Everything the calculation used is
/// frozen on the bill — assessed value, classification, the date rules were
/// resolved on, the discount-stacking option — and every line names its rule
/// and rate, so a bill keeps its meaning after rules change (CLAUDE.md §31,
/// §77). The total is always the sum of <see cref="Details"/>; it is never
/// stored separately.
///
/// Draft → Posted → Cancelled. Bills are never edited or deleted: a
/// recomputation is a new bill, and posting it cancels the previously posted
/// bill for the same RPU and tax year (<see cref="SupersededByBillId"/>).
/// </summary>
public sealed class TaxBill : AuditableEntity
{
    public Guid PropertyId { get; set; }
    public PropertyEntity? Property { get; set; }
    public Guid RpuId { get; set; }
    public RealPropertyUnit? Rpu { get; set; }
    public Guid TaxDeclarationId { get; set; }
    public TaxDeclaration? TaxDeclaration { get; set; }
    public Guid AssessmentId { get; set; }
    public Assessment? Assessment { get; set; }

    /// <summary>
    /// Assigned on posting from the TaxBill numbering scheme in force, if any
    /// (docs/FORMS-REVISION-PLAN.md §4.4); null when none is configured.
    /// </summary>
    public string? BillNumber { get; set; }

    public int TaxYear { get; set; }
    /// <summary>The bill assumes full payment on this date (discounts, penalty, interest).</summary>
    public DateOnly AsOfDate { get; set; }
    /// <summary>The date the rules in force were resolved on.</summary>
    public DateOnly RulesAsOfDate { get; set; }

    /// <summary>Frozen from the assessment.</summary>
    public decimal AssessedValue { get; set; }
    /// <summary>Frozen from the Tax Declaration; selected the tax rates.</summary>
    public Guid ClassificationId { get; set; }
    public Classification? Classification { get; set; }
    /// <summary>Frozen engine option (docs/BILLING.md §3.4).</summary>
    public bool DiscountStackingAllowed { get; set; }
    /// <summary>Calculator notes, e.g. a cap not applied for lack of a baseline.</summary>
    public string? Notes { get; set; }

    public WorkflowStatus Status { get; set; } = WorkflowStatus.Draft;
    public Guid? PostedBy { get; set; }
    public DateTimeOffset? PostedAt { get; set; }
    public Guid? CancelledBy { get; set; }
    public DateTimeOffset? CancelledAt { get; set; }
    public string? CancellationReason { get; set; }
    public Guid? SupersededByBillId { get; set; }

    public List<TaxBillTaxType> TaxTypes { get; set; } = [];
    public List<TaxBillDetail> Details { get; set; } = [];
}

/// <summary>
/// How one tax type's annual tax was reached on a bill, including any cap.
/// A posted bill's <see cref="AnnualTax"/> is the baseline later caps are
/// measured against.
/// </summary>
public sealed class TaxBillTaxType : Entity
{
    public Guid TaxBillId { get; set; }
    public Guid TaxTypeId { get; set; }
    public TaxType? TaxType { get; set; }
    public Guid TaxRateId { get; set; }
    public decimal RatePercent { get; set; }
    public decimal ComputedAnnualTax { get; set; }
    public Guid? CapRuleId { get; set; }
    public decimal? CapBaselineTax { get; set; }
    public decimal? CapLimit { get; set; }
    public decimal AnnualTax { get; set; }
}

/// <summary>
/// One bill line. <see cref="RuleId"/> points into the rule table implied by
/// <see cref="Component"/> (TaxRate, DiscountRule, PenaltyRule, InterestRule)
/// — no FK, since it spans tables; the rules themselves are never deleted.
/// </summary>
public sealed class TaxBillDetail : Entity
{
    public Guid TaxBillId { get; set; }
    public int LineNumber { get; set; }
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
    public string Explanation { get; set; } = string.Empty;
}
