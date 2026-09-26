using Prime.Domain.Entities.Billing;
using Prime.Domain.Enums;

namespace Prime.Domain.DomainServices;

/// <summary>What <see cref="CollectionCalculator"/> allocates: the payment date, the posted bills involved and the installments chosen.</summary>
public sealed record CollectionInput(DateOnly PaymentDate, IReadOnlyList<CollectionBill> Bills, IReadOnlyList<CollectionItem> Items);

/// <summary>
/// One posted bill as collection sees it: the rules frozen on it (the rule ids
/// on its lines, as of its <c>RulesAsOfDate</c>) and what is owed and paid
/// per installment and tax type. <see cref="Keys"/> are in bill order.
/// </summary>
public sealed record CollectionBill(
    Guid BillId,
    Guid RpuId,
    int TaxYear,
    bool AllowDiscountStacking,
    IReadOnlyList<DiscountRule> DiscountRules,
    IReadOnlyList<PenaltyRule> PenaltyRules,
    IReadOnlyList<InterestRule> InterestRules,
    IReadOnlyList<CollectionKey> Keys);

/// <summary>
/// One installment of one tax type (docs/analysis/collection.md §2).
/// <see cref="PrincipalPaid"/> counts payments that still stand, across every
/// bill that has been posted for this unit and tax year.
/// </summary>
public sealed record CollectionKey(
    int InstallmentSequence,
    Guid TaxTypeId,
    Guid TaxRateId,
    DateOnly DueDate,
    decimal PrincipalOwed,
    decimal PrincipalPaid,
    bool FixedPenaltyCharged = false)
{
    /// <summary>Never negative: a key paid beyond a lowered bill shows as overpaid elsewhere, not as a credit here.</summary>
    public decimal Outstanding => Math.Max(0m, PrincipalOwed - PrincipalPaid);
}

/// <summary>A selected installment: whole (all its tax types) or, with <see cref="PrincipalAmount"/>, a part of its principal.</summary>
public sealed record CollectionItem(Guid RpuId, int TaxYear, int InstallmentSequence, decimal? PrincipalAmount);

/// <summary>
/// One allocation line. Tax lines carry the principal settled
/// (<see cref="BaseAmount"/> = what was still owed); charge lines name their
/// rule and frozen rate like bill lines. Discounts are negative.
/// </summary>
public sealed record CollectionAllocation(
    Guid BillId,
    Guid RpuId,
    int TaxYear,
    int InstallmentSequence,
    Guid TaxTypeId,
    DateOnly DueDate,
    BillingComponent Component,
    Guid RuleId,
    decimal? RatePercent,
    decimal BaseAmount,
    decimal Amount,
    int? Months,
    CollectionYearCategory YearCategory,
    string Explanation);

public sealed record CollectionResult(IReadOnlyList<CollectionAllocation> Allocations)
{
    public decimal Total => Allocations.Sum(a => a.Amount);
    public decimal Principal => Allocations.Where(a => a.Component == BillingComponent.Tax).Sum(a => a.Amount);
}

public sealed record CollectionProblem(CollectionProblemKind Kind, string Message);

public enum CollectionProblemKind
{
    NothingSelected,
    DuplicateItem,
    InvalidBills,
    UnknownInstallment,
    AlreadySettled,
    InvalidAmount,
}
