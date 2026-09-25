using Prime.Domain.Enums;

namespace Prime.Domain.DomainServices;

/// <summary>
/// The output of <see cref="BillingCalculator.Calculate"/>: the annual tax
/// per tax type (with any cap) and the bill lines. <see cref="Total"/> is
/// always the sum of the lines (docs/BILLING.md §4).
/// </summary>
public sealed record BillingCalculationResult(
    IReadOnlyList<BillingTaxTypeResult> TaxTypes,
    IReadOnlyList<BillingLine> Lines,
    IReadOnlyList<string> Notes)
{
    public decimal Total => Lines.Sum(l => l.Amount);
}

/// <summary>
/// How one tax type's annual tax was reached. When a cap was applied,
/// <see cref="ComputedAnnualTax"/> is the uncapped amount and
/// <see cref="AnnualTax"/> the capped one, so the reduction is explainable
/// (CLAUDE.md §31).
/// </summary>
public sealed record BillingTaxTypeResult(
    Guid TaxTypeId,
    Guid TaxRateId,
    decimal RatePercent,
    decimal AssessedValue,
    decimal ComputedAnnualTax,
    Guid? CapRuleId,
    decimal? CapBaselineTax,
    decimal? CapLimit,
    decimal AnnualTax,
    IReadOnlyList<BillingTaxTypeLine> Lines);

/// <summary>
/// The tax one assessment line bears for one tax type: its assessed value ×
/// the rate for its classification, rounded. The tax type's computed annual
/// tax is the sum of its lines (docs/analysis/mrpaao-forms-model.md §8.4).
/// </summary>
public sealed record BillingTaxTypeLine(Guid? ClassificationId, decimal AssessedValue, Guid TaxRateId, decimal RatePercent, decimal Tax);

/// <summary>
/// One bill line. <see cref="RuleId"/> identifies the rule of the table
/// implied by <see cref="Component"/> (TaxRate, DiscountRule, PenaltyRule,
/// InterestRule); <see cref="RatePercent"/> freezes the rate it used.
/// Discounts are negative.
/// </summary>
public sealed record BillingLine(
    int InstallmentSequence,
    DateOnly DueDate,
    Guid TaxTypeId,
    BillingComponent Component,
    Guid RuleId,
    decimal? RatePercent,
    decimal BaseAmount,
    decimal Amount,
    int? Months,
    string Explanation);
