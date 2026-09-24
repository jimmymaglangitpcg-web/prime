using FluentValidation;
using Prime.Domain.Enums;

namespace Prime.Application.Features.Billing.Rules;

internal static class BillingRuleValidation
{
    public static void ApplyCommonRules<T>(this AbstractValidator<T> validator) where T : IBillingRuleRequest
    {
        validator.RuleFor(x => x.LegalBasis).NotEmpty().MaximumLength(500)
            .WithMessage("legalBasis is required (max 500) — every billing rule must cite its legal basis (CLAUDE.md §6).");
        validator.RuleFor(x => x.OrdinanceNumber).NotEmpty().MaximumLength(100);
        validator.RuleFor(x => x.EffectiveDate).NotEqual(default(DateOnly)).WithMessage("effectiveDate is required.");
        validator.RuleFor(x => x.Remarks).MaximumLength(1000);
    }

    public static IRuleBuilderOptions<T, decimal> Percent<T>(this IRuleBuilder<T, decimal> rule) =>
        rule.InclusiveBetween(0m, 100m).WithMessage("{PropertyName} must be a percent between 0 and 100.");

    /// <summary>Month/day must exist in every year (so 29 February is rejected).</summary>
    public static bool IsValidMonthDay(int month, int day) =>
        month is >= 1 and <= 12 && day >= 1 && day <= DateTime.DaysInMonth(2001, month);
}

public sealed class CreateTaxRateRequestValidator : AbstractValidator<CreateTaxRateRequest>
{
    public CreateTaxRateRequestValidator()
    {
        this.ApplyCommonRules();
        RuleFor(x => x.TaxTypeId).NotEmpty();
        RuleFor(x => x.Rate).Percent();
    }
}

public sealed class CreatePaymentScheduleRequestValidator : AbstractValidator<CreatePaymentScheduleRequest>
{
    public CreatePaymentScheduleRequestValidator()
    {
        this.ApplyCommonRules();
        RuleFor(x => x.Installments).NotEmpty();
        RuleForEach(x => x.Installments).ChildRules(i =>
        {
            i.RuleFor(x => x).Must(x => BillingRuleValidation.IsValidMonthDay(x.DueMonth, x.DueDay))
                .WithMessage("Installment due month/day must be a date that exists every year.");
            i.RuleFor(x => x.SharePercent).GreaterThan(0m).LessThanOrEqualTo(100m);
        });
        RuleFor(x => x.Installments)
            .Must(list => list.Select(i => i.Sequence).OrderBy(s => s).SequenceEqual(Enumerable.Range(1, list.Count)))
            .When(x => x.Installments is { Count: > 0 })
            .WithMessage("Installment sequences must be 1..n with no gaps or duplicates.");
        RuleFor(x => x.Installments)
            .Must(list => list.Sum(i => i.SharePercent) == 100m)
            .When(x => x.Installments is { Count: > 0 })
            .WithMessage("Installment shares must total exactly 100.");
        RuleFor(x => x.Installments)
            .Must(list =>
            {
                var ordered = list.OrderBy(i => i.Sequence).Select(i => (i.DueMonth, i.DueDay)).ToList();
                return ordered.Zip(ordered.Skip(1)).All(p => p.First.CompareTo(p.Second) < 0);
            })
            .When(x => x.Installments is { Count: > 1 })
            .WithMessage("Installment due dates must increase with sequence.");
    }
}

public sealed class CreateDiscountRuleRequestValidator : AbstractValidator<CreateDiscountRuleRequest>
{
    public CreateDiscountRuleRequestValidator()
    {
        this.ApplyCommonRules();
        RuleFor(x => x.Kind).IsInEnum();
        RuleFor(x => x.Rate).GreaterThan(0m).LessThanOrEqualTo(100m);
        When(x => x.Kind == DiscountKind.AdvancePayment, () =>
        {
            RuleFor(x => x).Must(x => x.CutoffMonth is { } m && x.CutoffDay is { } d && BillingRuleValidation.IsValidMonthDay(m, d))
                .WithMessage("An advance-payment discount needs a valid cutoffMonth/cutoffDay.");
            RuleFor(x => x.CutoffYearOffset).NotNull().InclusiveBetween(-1, 0)
                .WithMessage("cutoffYearOffset must be -1 (year before the tax year) or 0 (the tax year).");
        });
        When(x => x.Kind == DiscountKind.PromptPayment, () =>
            RuleFor(x => x).Must(x => x.CutoffMonth is null && x.CutoffDay is null && x.CutoffYearOffset is null)
                .WithMessage("A prompt-payment discount uses each installment's due date; cutoff fields must be empty."));
    }
}

public sealed class CreateInterestRuleRequestValidator : AbstractValidator<CreateInterestRuleRequest>
{
    public CreateInterestRuleRequestValidator()
    {
        this.ApplyCommonRules();
        RuleFor(x => x.RatePerMonth).GreaterThan(0m).LessThanOrEqualTo(100m);
        RuleFor(x => x.MaxMonths).GreaterThan(0).When(x => x.MaxMonths is not null);
        RuleFor(x => x.MonthCounting).IsInEnum();
    }
}

public sealed class CreatePenaltyRuleRequestValidator : AbstractValidator<CreatePenaltyRuleRequest>
{
    public CreatePenaltyRuleRequestValidator()
    {
        this.ApplyCommonRules();
        RuleFor(x => x).Must(x => (x.Rate is null) != (x.FixedAmount is null))
            .WithMessage("Give exactly one of rate or fixedAmount.");
        RuleFor(x => x.Rate!.Value).Percent().When(x => x.Rate is not null);
        RuleFor(x => x.FixedAmount!.Value).GreaterThanOrEqualTo(0m)
            .Must(v => decimal.Round(v, 2) == v).WithMessage("fixedAmount must have at most 2 decimal places.")
            .When(x => x.FixedAmount is not null);
        RuleFor(x => x.AppliesAfterDays).GreaterThanOrEqualTo(0);
    }
}

public sealed class CreateTaxIncreaseCapRuleRequestValidator : AbstractValidator<CreateTaxIncreaseCapRuleRequest>
{
    public CreateTaxIncreaseCapRuleRequestValidator()
    {
        this.ApplyCommonRules();
        RuleFor(x => x.SmvId).NotEmpty();
        RuleFor(x => x.Basis).IsInEnum();
        RuleFor(x => x.Baseline).IsInEnum();
        RuleFor(x => x.MaxIncreasePercent).GreaterThanOrEqualTo(0m).LessThan(1000m)
            .WithMessage("maxIncreasePercent must be at least 0 and below 1000.");
        RuleFor(x => x.EndDate).GreaterThanOrEqualTo(x => x.EffectiveDate)
            .WithMessage("endDate (inclusive) must be on or after effectiveDate.");
        When(x => x.Basis == TaxIncreaseCapBasis.StatutoryFirstYear, () =>
        {
            // RA 12001 §29 ¶3 / IRR §55: "for the first year of effectivity of the approved SMV",
            // measured against the tax assessed before that SMV.
            RuleFor(x => x.EndDate).LessThan(x => x.EffectiveDate.AddYears(1))
                .WithMessage("The statutory first-year cap covers at most one year from its effective date.");
            RuleFor(x => x.Baseline).Equal(TaxIncreaseCapBaseline.TaxBeforeSmv)
                .WithMessage("The statutory first-year cap is measured against the tax assessed before the SMV (baseline TaxBeforeSmv).");
        });
    }
}
