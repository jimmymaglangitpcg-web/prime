using Prime.Domain.Common;
using Prime.Domain.Entities.Reference;
using Prime.Domain.Enums;

namespace Prime.Domain.Entities.Billing;

/// <summary>
/// Common shape of every billing rule (docs/BILLING.md §3; CLAUDE.md §44).
/// Ordinance fields are inline — the <see cref="AssessmentLevel"/>/<see cref="Smv"/>
/// precedent, since no Ordinance table exists. Only <see cref="WorkflowStatus.Approved"/>
/// rules are ever applied. <see cref="EndDate"/> is the INCLUSIVE last day
/// (AssessmentLevel convention). A rule's predecessor is closed when the rule is
/// approved — not when it is drafted — and approved rules are never edited or
/// deleted; a correction is a new rule (CLAUDE.md §49/§76).
/// </summary>
public abstract class BillingRule : AuditableEntity
{
    /// <summary>E.g. "RA 7160 §233; Ord. No. 2026-012 §3". Required — CLAUDE.md §6.</summary>
    public string LegalBasis { get; set; } = string.Empty;
    public string OrdinanceNumber { get; set; } = string.Empty;
    public DateOnly? OrdinanceDate { get; set; }

    public DateOnly EffectiveDate { get; set; }
    public DateOnly? EndDate { get; set; }

    public WorkflowStatus Status { get; set; } = WorkflowStatus.Draft;
    public Guid? ApprovedBy { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
    public string? Remarks { get; set; }
}

/// <summary>
/// Basic RPT or an additional levy, as a percent (0–100) of assessed value.
/// Scope key: (TaxTypeId, ClassificationId); a classification-specific rate
/// takes precedence over the general (null) one for the same tax type.
/// </summary>
public sealed class TaxRate : BillingRule
{
    public Guid TaxTypeId { get; set; }
    public TaxType? TaxType { get; set; }
    public Guid? ClassificationId { get; set; }
    public Classification? Classification { get; set; }
    public decimal Rate { get; set; }
}

/// <summary>Installment due dates within the tax year. One schedule in force at a time.</summary>
public sealed class PaymentSchedule : BillingRule
{
    public List<PaymentScheduleInstallment> Installments { get; set; } = [];
}

public sealed class PaymentScheduleInstallment : Entity
{
    public Guid PaymentScheduleId { get; set; }
    public int Sequence { get; set; }
    public int DueMonth { get; set; }
    public int DueDay { get; set; }
    /// <summary>Share of the annual tax due in this installment; a schedule's shares total exactly 100.</summary>
    public decimal SharePercent { get; set; }
}

/// <summary>Scope key: (Kind, TaxTypeId); null tax type = every tax type on the bill.</summary>
public sealed class DiscountRule : BillingRule
{
    public Guid? TaxTypeId { get; set; }
    public TaxType? TaxType { get; set; }
    public DiscountKind Kind { get; set; }
    public decimal Rate { get; set; }

    /// <summary>ADVANCE_PAYMENT only: the whole year must be paid on/before this month/day…</summary>
    public int? CutoffMonth { get; set; }
    public int? CutoffDay { get; set; }
    /// <summary>…of (tax year + offset): −1 = the year before the tax year, 0 = the tax year.</summary>
    public int? CutoffYearOffset { get; set; }
}

/// <summary>Interest on unpaid overdue tax. Scope key: (TaxTypeId).</summary>
public sealed class InterestRule : BillingRule
{
    public Guid? TaxTypeId { get; set; }
    public TaxType? TaxType { get; set; }
    public decimal RatePerMonth { get; set; }
    /// <summary>Cap on months counted; null = no cap.</summary>
    public int? MaxMonths { get; set; }
    public InterestMonthCounting MonthCounting { get; set; }
}

/// <summary>Ordinance surcharge on overdue tax, if any. Exactly one of Rate / FixedAmount. Scope key: (TaxTypeId).</summary>
public sealed class PenaltyRule : BillingRule
{
    public Guid? TaxTypeId { get; set; }
    public TaxType? TaxType { get; set; }
    public decimal? Rate { get; set; }
    public decimal? FixedAmount { get; set; }
    public int AppliesAfterDays { get; set; }
}

/// <summary>
/// Cap on how much a property's tax may rise because of a new SMV
/// (RA 12001 §29 ¶3; IRR §55). Applied to each tax type separately — never
/// to the bill total — so a null <see cref="TaxTypeId"/> means "every tax
/// type, each on its own". Unlike the other rules a cap covers a bounded
/// window: <see cref="BillingRule.EndDate"/> is set at creation.
/// Scope key: (SmvId, TaxTypeId, Basis).
/// </summary>
public sealed class TaxIncreaseCapRule : BillingRule
{
    /// <summary>The SMV revision whose increases are capped.</summary>
    public Guid SmvId { get; set; }
    public Smv? Smv { get; set; }
    public Guid? TaxTypeId { get; set; }
    public TaxType? TaxType { get; set; }
    public TaxIncreaseCapBasis Basis { get; set; }
    public TaxIncreaseCapBaseline Baseline { get; set; }
    /// <summary>Largest allowed increase, as a percent of the baseline tax. Configured with its legal basis — not a code constant.</summary>
    public decimal MaxIncreasePercent { get; set; }
}
