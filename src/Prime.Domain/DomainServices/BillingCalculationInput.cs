using Prime.Domain.Entities.Billing;
using Prime.Domain.Enums;

namespace Prime.Domain.DomainServices;

/// <summary>
/// Everything <see cref="BillingCalculator"/> needs for one RPU and tax year.
/// The caller (the database-facing billing service) resolves which approved
/// rules are in force; the calculator only chooses among the rules it is
/// given (e.g. a classification-specific rate over the general one) and
/// never loads anything itself.
/// </summary>
public sealed record BillingCalculationInput
{
    /// <summary>Assessed value from the posted assessment. Must not be negative; equals Σ <see cref="Lines"/> when lines are given.</summary>
    public required decimal AssessedValue { get; init; }

    /// <summary>The property's classification; selects classification-specific tax rates when no <see cref="Lines"/> are given.</summary>
    public Guid? ClassificationId { get; init; }

    /// <summary>
    /// The assessment's lines (docs/analysis/mrpaao-forms-model.md §8.4): each
    /// is taxed at the rate for its own classification. Empty: one line of
    /// <see cref="AssessedValue"/> and <see cref="ClassificationId"/>.
    /// </summary>
    public IReadOnlyList<BillingAssessmentLine> Lines { get; init; } = [];

    internal IReadOnlyList<BillingAssessmentLine> EffectiveLines =>
        Lines.Count > 0 ? Lines : [new BillingAssessmentLine(ClassificationId, AssessedValue)];

    public required int TaxYear { get; init; }

    /// <summary>
    /// The bill assumes the whole amount is paid on this date: it decides
    /// which discounts still apply and how much penalty/interest has accrued.
    /// </summary>
    public required DateOnly AsOfDate { get; init; }

    public required IReadOnlyList<TaxRate> TaxRates { get; init; }
    public required PaymentSchedule PaymentSchedule { get; init; }
    public IReadOnlyList<DiscountRule> DiscountRules { get; init; } = [];
    public IReadOnlyList<InterestRule> InterestRules { get; init; } = [];
    public IReadOnlyList<PenaltyRule> PenaltyRules { get; init; } = [];
    public IReadOnlyList<TaxIncreaseCapRule> TaxIncreaseCapRules { get; init; } = [];

    /// <summary>
    /// Baseline tax per tax type for <see cref="TaxIncreaseCapRules"/>. A cap
    /// with no matching baseline is not applied and the result says so
    /// (docs/BILLING.md §3.7: properties with no prior tax).
    /// </summary>
    public IReadOnlyList<CapBaselineTax> CapBaselines { get; init; } = [];
}

/// <summary>One assessment line to tax: its classification and assessed value.</summary>
public sealed record BillingAssessmentLine(Guid? ClassificationId, decimal AssessedValue);

/// <summary>The tax a cap is measured against, for one tax type and baseline kind.</summary>
public sealed record CapBaselineTax(Guid TaxTypeId, TaxIncreaseCapBaseline Baseline, decimal Amount);

/// <summary>
/// Engine settings that are policy choices awaiting LGU confirmation
/// (docs/BILLING.md §3.4), passed in rather than fixed in code.
/// </summary>
/// <param name="AllowDiscountStacking">
/// false: when a prompt-payment and an advance-payment discount both apply,
/// only the larger is given. true: both are given.
/// </param>
public sealed record BillingCalculationOptions(bool AllowDiscountStacking);
