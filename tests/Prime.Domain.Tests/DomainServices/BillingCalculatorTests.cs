using Prime.Domain.DomainServices;
using Prime.Domain.Entities.Billing;
using Prime.Domain.Enums;
using Shouldly;
using Xunit;

namespace Prime.Domain.Tests.DomainServices;

/// <summary>
/// CLAUDE.md §75 financial cases for <see cref="BillingCalculator"/>. Every
/// rate, date and amount below is DEMO test data (CLAUDE.md §81), chosen to
/// make the arithmetic easy to check — none is a real ordinance value.
/// </summary>
public class BillingCalculatorTests
{
    private static readonly Guid DemoBasic = Guid.NewGuid();
    private static readonly Guid DemoSef = Guid.NewGuid();
    private static readonly Guid DemoResidential = Guid.NewGuid();
    private static readonly Guid DemoCommercial = Guid.NewGuid();
    private static readonly BillingCalculationOptions NoStacking = new(AllowDiscountStacking: false);

    private static T Approved<T>(T rule) where T : BillingRule
    {
        rule.LegalBasis = "DEMO";
        rule.OrdinanceNumber = "DEMO-ORD";
        rule.EffectiveDate = new DateOnly(2020, 1, 1);
        rule.Status = WorkflowStatus.Approved;
        return rule;
    }

    private static TaxRate Rate(Guid taxType, decimal percent, Guid? classification = null) =>
        Approved(new TaxRate { TaxTypeId = taxType, Rate = percent, ClassificationId = classification });

    private static PaymentSchedule Schedule(params (int Month, int Day, decimal Share)[] installments) =>
        Approved(new PaymentSchedule
        {
            Installments = installments.Select((x, i) => new PaymentScheduleInstallment
            {
                Sequence = i + 1, DueMonth = x.Month, DueDay = x.Day, SharePercent = x.Share,
            }).ToList(),
        });

    private static PaymentSchedule Annual() => Schedule((3, 31, 100m));

    private static BillingCalculationInput Input(decimal assessedValue, DateOnly asOf, params TaxRate[] rates) => new()
    {
        AssessedValue = assessedValue,
        TaxYear = 2026,
        AsOfDate = asOf,
        TaxRates = rates,
        PaymentSchedule = Annual(),
    };

    private static decimal Sum(BillingCalculationResult r, BillingComponent c) =>
        r.Lines.Where(l => l.Component == c).Sum(l => l.Amount);

    // --- Basic tax ---

    [Fact]
    public void Calculate_SingleTaxType_IsAssessedValueTimesRate()
    {
        var result = BillingCalculator.Calculate(Input(100_000m, new(2026, 1, 15), Rate(DemoBasic, 1m)), NoStacking);

        result.TaxTypes.Single().AnnualTax.ShouldBe(1_000m);
        result.Lines.Single().Component.ShouldBe(BillingComponent.Tax);
        result.Total.ShouldBe(1_000m);
    }

    [Fact]
    public void Calculate_TwoTaxTypes_EachBilledSeparately()
    {
        var result = BillingCalculator.Calculate(
            Input(100_000m, new(2026, 1, 15), Rate(DemoBasic, 1m), Rate(DemoSef, 0.5m)), NoStacking);

        result.TaxTypes.Select(t => (t.TaxTypeId, t.AnnualTax)).ShouldBe([(DemoBasic, 1_000m), (DemoSef, 500m)]);
        result.Total.ShouldBe(1_500m);
    }

    [Fact]
    public void Calculate_ClassificationSpecificRate_WinsOverGeneral_OtherClassificationIgnored()
    {
        var input = Input(100_000m, new(2026, 1, 15),
            Rate(DemoBasic, 1m), Rate(DemoBasic, 2m, DemoResidential), Rate(DemoBasic, 3m, DemoCommercial))
            with { ClassificationId = DemoResidential };

        var result = BillingCalculator.Calculate(input, NoStacking);

        result.TaxTypes.Single().RatePercent.ShouldBe(2m);
        result.Total.ShouldBe(2_000m);
    }

    [Fact]
    public void Calculate_NoClassification_UsesGeneralRateOnly()
    {
        var result = BillingCalculator.Calculate(
            Input(100_000m, new(2026, 1, 15), Rate(DemoBasic, 1m), Rate(DemoBasic, 2m, DemoResidential)), NoStacking);

        result.Total.ShouldBe(1_000m);
    }

    // --- §75: zero, large amounts, decimals, rounding ---

    [Fact]
    public void Calculate_ZeroAssessedValue_IsZeroNotAnError()
    {
        var result = BillingCalculator.Calculate(Input(0m, new(2026, 1, 15), Rate(DemoBasic, 1m)), NoStacking);

        result.Total.ShouldBe(0m);
        result.Lines.Single().Amount.ShouldBe(0m);
    }

    [Fact]
    public void Calculate_ZeroRate_IsZero()
    {
        var result = BillingCalculator.Calculate(Input(100_000m, new(2026, 1, 15), Rate(DemoBasic, 0m)), NoStacking);

        result.Total.ShouldBe(0m);
    }

    [Fact]
    public void Calculate_LargeAssessedValue_IsExact()
    {
        var result = BillingCalculator.Calculate(
            Input(9_999_999_999_999.99m, new(2026, 1, 15), Rate(DemoBasic, 2m)), NoStacking);

        result.Total.ShouldBe(200_000_000_000.00m); // 199,999,999,999.9998 rounded to centavos
    }

    [Fact]
    public void Calculate_FractionalCentavos_RoundToTwoDecimals()
    {
        var result = BillingCalculator.Calculate(Input(333.33m, new(2026, 1, 15), Rate(DemoBasic, 1m)), NoStacking);

        result.Total.ShouldBe(3.33m); // 3.3333
    }

    [Fact]
    public void Calculate_MidpointRoundsAwayFromZero()
    {
        var result = BillingCalculator.Calculate(Input(12.5m, new(2026, 1, 15), Rate(DemoBasic, 1m)), NoStacking);

        result.Total.ShouldBe(0.13m); // 0.125 — banker's rounding would give 0.12
    }

    [Fact]
    public void Calculate_FractionalRate_IsExact()
    {
        var result = BillingCalculator.Calculate(Input(123_456.78m, new(2026, 1, 15), Rate(DemoBasic, 0.375m)), NoStacking);

        result.Total.ShouldBe(462.96m); // 462.962925
    }

    // --- Installments ---

    [Fact]
    public void Calculate_QuarterlyInstallments_SplitEvenly()
    {
        var input = Input(100_000m, new(2026, 1, 15), Rate(DemoBasic, 1m)) with
        {
            PaymentSchedule = Schedule((3, 31, 25m), (6, 30, 25m), (9, 30, 25m), (12, 31, 25m)),
        };

        var result = BillingCalculator.Calculate(input, NoStacking);

        result.Lines.Select(l => (l.InstallmentSequence, l.DueDate, l.Amount)).ShouldBe([
            (1, new DateOnly(2026, 3, 31), 250m), (2, new DateOnly(2026, 6, 30), 250m),
            (3, new DateOnly(2026, 9, 30), 250m), (4, new DateOnly(2026, 12, 31), 250m)]);
    }

    [Fact]
    public void Calculate_UnevenSplit_LastInstallmentTakesRemainder_SoInstallmentsTotalAnnualTax()
    {
        var input = Input(10m, new(2026, 1, 15), Rate(DemoBasic, 1m)) with // annual tax 0.10
        {
            PaymentSchedule = Schedule((3, 31, 33.33m), (6, 30, 33.33m), (9, 30, 33.34m)),
        };

        var result = BillingCalculator.Calculate(input, NoStacking);

        result.Lines.Select(l => l.Amount).ShouldBe([0.03m, 0.03m, 0.04m]);
        result.Total.ShouldBe(0.10m);
    }

    [Fact]
    public void Calculate_InstallmentsOutOfOrder_AreBilledBySequence()
    {
        var schedule = Schedule((3, 31, 50m), (9, 30, 50m));
        schedule.Installments.Reverse();
        var input = Input(1_000m, new(2026, 1, 15), Rate(DemoBasic, 1m)) with { PaymentSchedule = schedule };

        var result = BillingCalculator.Calculate(input, NoStacking);

        result.Lines.Select(l => l.InstallmentSequence).ShouldBe([1, 2]);
    }

    // --- Tax increase cap (RA 12001 §29 ¶3 — percentages here are DEMO) ---

    private static TaxIncreaseCapRule Cap(decimal percent, Guid? taxType = null,
        TaxIncreaseCapBaseline baseline = TaxIncreaseCapBaseline.TaxBeforeSmv) =>
        Approved(new TaxIncreaseCapRule { TaxTypeId = taxType, MaxIncreasePercent = percent, Baseline = baseline, SmvId = Guid.NewGuid() });

    [Fact]
    public void Calculate_CapBinding_LimitsAnnualTaxAndRecordsWhy()
    {
        var cap = Cap(6m);
        var input = Input(100_000m, new(2026, 1, 15), Rate(DemoBasic, 1m)) with
        {
            TaxIncreaseCapRules = [cap],
            CapBaselines = [new(DemoBasic, TaxIncreaseCapBaseline.TaxBeforeSmv, 900m)],
        };

        var taxType = BillingCalculator.Calculate(input, NoStacking).TaxTypes.Single();

        taxType.ComputedAnnualTax.ShouldBe(1_000m);
        taxType.CapLimit.ShouldBe(954m);
        taxType.AnnualTax.ShouldBe(954m);
        taxType.CapRuleId.ShouldBe(cap.Id);
        taxType.CapBaselineTax.ShouldBe(900m);
    }

    [Fact]
    public void Calculate_CapNotBinding_KeepsComputedTax()
    {
        var input = Input(95_000m, new(2026, 1, 15), Rate(DemoBasic, 1m)) with
        {
            TaxIncreaseCapRules = [Cap(6m)],
            CapBaselines = [new(DemoBasic, TaxIncreaseCapBaseline.TaxBeforeSmv, 900m)],
        };

        BillingCalculator.Calculate(input, NoStacking).TaxTypes.Single().AnnualTax.ShouldBe(950m);
    }

    [Fact]
    public void Calculate_CapAppliesPerTaxType_NotToBillTotal()
    {
        var input = Input(100_000m, new(2026, 1, 15), Rate(DemoBasic, 1m), Rate(DemoSef, 1m)) with
        {
            TaxIncreaseCapRules = [Cap(6m)],
            CapBaselines = [
                new(DemoBasic, TaxIncreaseCapBaseline.TaxBeforeSmv, 900m),  // capped to 954
                new(DemoSef, TaxIncreaseCapBaseline.TaxBeforeSmv, 1_000m)], // limit 1,060, not binding
        };

        var result = BillingCalculator.Calculate(input, NoStacking);

        result.TaxTypes.Select(t => t.AnnualTax).ShouldBe([954m, 1_000m]);
        result.Total.ShouldBe(1_954m);
    }

    [Fact]
    public void Calculate_TaxTypeSpecificCap_WinsOverGeneralCap()
    {
        var input = Input(100_000m, new(2026, 1, 15), Rate(DemoBasic, 1m)) with
        {
            TaxIncreaseCapRules = [Cap(6m), Cap(0m, DemoBasic)],
            CapBaselines = [new(DemoBasic, TaxIncreaseCapBaseline.TaxBeforeSmv, 900m)],
        };

        BillingCalculator.Calculate(input, NoStacking).TaxTypes.Single().AnnualTax.ShouldBe(900m);
    }

    [Fact]
    public void Calculate_CapWithoutMatchingBaseline_IsNotApplied_AndSaysSo()
    {
        var input = Input(100_000m, new(2026, 1, 15), Rate(DemoBasic, 1m)) with
        {
            TaxIncreaseCapRules = [Cap(6m)],
            CapBaselines = [new(DemoBasic, TaxIncreaseCapBaseline.PreviousTaxYear, 900m)], // wrong baseline kind
        };

        var result = BillingCalculator.Calculate(input, NoStacking);

        result.TaxTypes.Single().AnnualTax.ShouldBe(1_000m);
        result.TaxTypes.Single().CapRuleId.ShouldBeNull();
        result.Notes.Single().ShouldContain("not applied");
    }

    // --- Discounts (rates DEMO) ---

    private static DiscountRule Prompt(decimal rate, Guid? taxType = null) =>
        Approved(new DiscountRule { Kind = DiscountKind.PromptPayment, Rate = rate, TaxTypeId = taxType });

    private static DiscountRule Advance(decimal rate, int month, int day, int yearOffset) =>
        Approved(new DiscountRule { Kind = DiscountKind.AdvancePayment, Rate = rate, CutoffMonth = month, CutoffDay = day, CutoffYearOffset = yearOffset });

    [Fact]
    public void Calculate_PromptPaymentOnDueDate_GetsDiscount()
    {
        var input = Input(100_000m, new(2026, 3, 31), Rate(DemoBasic, 1m)) with { DiscountRules = [Prompt(10m)] };

        var result = BillingCalculator.Calculate(input, NoStacking);

        Sum(result, BillingComponent.Discount).ShouldBe(-100m);
        result.Total.ShouldBe(900m);
    }

    [Fact]
    public void Calculate_AfterDueDate_NoDiscount()
    {
        var input = Input(100_000m, new(2026, 4, 1), Rate(DemoBasic, 1m)) with { DiscountRules = [Prompt(10m)] };

        Sum(BillingCalculator.Calculate(input, NoStacking), BillingComponent.Discount).ShouldBe(0m);
    }

    [Fact]
    public void Calculate_TaxTypeSpecificDiscount_WinsOverGeneral()
    {
        var input = Input(100_000m, new(2026, 1, 15), Rate(DemoBasic, 1m), Rate(DemoSef, 1m)) with
        {
            DiscountRules = [Prompt(10m), Prompt(20m, DemoSef)],
        };

        var discounts = BillingCalculator.Calculate(input, NoStacking).Lines.Where(l => l.Component == BillingComponent.Discount);

        discounts.Select(d => (d.TaxTypeId, d.Amount)).ShouldBe([(DemoBasic, -100m), (DemoSef, -200m)]);
    }

    [Fact]
    public void Calculate_AdvancePaymentBeforeCutoff_NoStacking_GivesLargerDiscountOnly()
    {
        var input = Input(100_000m, new(2025, 12, 15), Rate(DemoBasic, 1m)) with
        {
            DiscountRules = [Prompt(10m), Advance(20m, 12, 31, -1)],
        };

        var discount = BillingCalculator.Calculate(input, NoStacking).Lines.Single(l => l.Component == BillingComponent.Discount);

        discount.Amount.ShouldBe(-200m);
        discount.Explanation.ShouldContain("advance payment");
    }

    [Fact]
    public void Calculate_AdvancePaymentBeforeCutoff_Stacking_GivesBoth()
    {
        var input = Input(100_000m, new(2025, 12, 15), Rate(DemoBasic, 1m)) with
        {
            DiscountRules = [Prompt(10m), Advance(20m, 12, 31, -1)],
        };

        Sum(BillingCalculator.Calculate(input, new BillingCalculationOptions(AllowDiscountStacking: true)), BillingComponent.Discount)
            .ShouldBe(-300m);
    }

    [Fact]
    public void Calculate_AdvancePaymentAfterCutoff_OnlyPromptDiscount()
    {
        var input = Input(100_000m, new(2026, 1, 2), Rate(DemoBasic, 1m)) with
        {
            DiscountRules = [Prompt(10m), Advance(20m, 12, 31, -1)],
        };

        Sum(BillingCalculator.Calculate(input, NoStacking), BillingComponent.Discount).ShouldBe(-100m);
    }

    [Fact]
    public void Calculate_DiscountRounding_IsPerLine()
    {
        var input = Input(333.33m, new(2026, 1, 15), Rate(DemoBasic, 1m)) with { DiscountRules = [Prompt(10m)] };

        var result = BillingCalculator.Calculate(input, NoStacking);

        Sum(result, BillingComponent.Discount).ShouldBe(-0.33m); // 10% of 3.33
        result.Total.ShouldBe(3.00m);
    }

    // --- Interest and penalty (rates DEMO) ---

    private static InterestRule Interest(decimal perMonth, int? maxMonths, InterestMonthCounting counting) =>
        Approved(new InterestRule { RatePerMonth = perMonth, MaxMonths = maxMonths, MonthCounting = counting });

    [Theory]
    [InlineData(2026, 3, 31, InterestMonthCounting.FractionCountsAsFullMonth, 0)] // on the due date
    [InlineData(2026, 4, 1, InterestMonthCounting.FractionCountsAsFullMonth, 1)]
    [InlineData(2026, 4, 1, InterestMonthCounting.CompletedMonthsOnly, 0)]
    [InlineData(2026, 4, 30, InterestMonthCounting.CompletedMonthsOnly, 1)] // 31 Mar + 1 month = 30 Apr
    [InlineData(2026, 5, 1, InterestMonthCounting.FractionCountsAsFullMonth, 2)]
    [InlineData(2027, 3, 31, InterestMonthCounting.CompletedMonthsOnly, 12)]
    public void DelinquentMonths_CountsFromDueDate(int y, int m, int d, InterestMonthCounting counting, int expected) =>
        BillingCalculator.DelinquentMonths(new DateOnly(2026, 3, 31), new DateOnly(y, m, d), counting).ShouldBe(expected);

    [Fact]
    public void Calculate_Overdue_ChargesInterestPerMonth()
    {
        var input = Input(100_000m, new(2026, 6, 15), Rate(DemoBasic, 1m)) with
        {
            InterestRules = [Interest(2m, null, InterestMonthCounting.FractionCountsAsFullMonth)],
        };

        var interest = BillingCalculator.Calculate(input, NoStacking).Lines.Single(l => l.Component == BillingComponent.Interest);

        interest.Months.ShouldBe(3); // 31 Mar → 15 Jun: 2 full months + a fraction
        interest.Amount.ShouldBe(60m);
    }

    [Fact]
    public void Calculate_InterestMonths_AreCappedAtMaxMonths()
    {
        var input = Input(100_000m, new(2026, 6, 15), Rate(DemoBasic, 1m)) with
        {
            TaxYear = 2020,
            InterestRules = [Interest(2m, 36, InterestMonthCounting.FractionCountsAsFullMonth)],
        };

        var interest = BillingCalculator.Calculate(input, NoStacking).Lines.Single(l => l.Component == BillingComponent.Interest);

        interest.Months.ShouldBe(36);
        interest.Amount.ShouldBe(720m);
        interest.Explanation.ShouldContain("capped at 36 of 75");
    }

    [Fact]
    public void Calculate_Overdue_NoDiscountEvenIfConfigured()
    {
        var input = Input(100_000m, new(2026, 6, 15), Rate(DemoBasic, 1m)) with
        {
            DiscountRules = [Prompt(10m)],
            InterestRules = [Interest(2m, null, InterestMonthCounting.CompletedMonthsOnly)],
        };

        var result = BillingCalculator.Calculate(input, NoStacking);

        Sum(result, BillingComponent.Discount).ShouldBe(0m);
        Sum(result, BillingComponent.Interest).ShouldBe(40m);
        result.Total.ShouldBe(1_040m);
    }

    [Fact]
    public void Calculate_MixedInstallments_DiscountOnFutureInterestOnPast()
    {
        var input = Input(100_000m, new(2026, 5, 15), Rate(DemoBasic, 1m)) with
        {
            PaymentSchedule = Schedule((3, 31, 50m), (9, 30, 50m)),
            DiscountRules = [Prompt(10m)],
            InterestRules = [Interest(2m, null, InterestMonthCounting.FractionCountsAsFullMonth)],
        };

        var result = BillingCalculator.Calculate(input, NoStacking);

        result.Lines.Select(l => (l.InstallmentSequence, l.Component, l.Amount)).ShouldBe([
            (1, BillingComponent.Tax, 500m), (1, BillingComponent.Interest, 20m),
            (2, BillingComponent.Tax, 500m), (2, BillingComponent.Discount, -50m)]);
        result.Total.ShouldBe(970m);
    }

    [Fact]
    public void Calculate_PenaltyRate_AppliesOnlyAfterGraceDays()
    {
        var penalty = Approved(new PenaltyRule { Rate = 5m, AppliesAfterDays = 10 });
        var within = Input(100_000m, new(2026, 4, 10), Rate(DemoBasic, 1m)) with { PenaltyRules = [penalty] };
        var after = within with { AsOfDate = new(2026, 4, 11) };

        Sum(BillingCalculator.Calculate(within, NoStacking), BillingComponent.Penalty).ShouldBe(0m);
        Sum(BillingCalculator.Calculate(after, NoStacking), BillingComponent.Penalty).ShouldBe(50m);
    }

    [Fact]
    public void Calculate_PenaltyFixedAmount_ForItsTaxTypeOnly()
    {
        var penalty = Approved(new PenaltyRule { FixedAmount = 25.50m, TaxTypeId = DemoBasic });
        var input = Input(100_000m, new(2026, 4, 1), Rate(DemoBasic, 1m), Rate(DemoSef, 1m)) with { PenaltyRules = [penalty] };

        var penalties = BillingCalculator.Calculate(input, NoStacking).Lines.Where(l => l.Component == BillingComponent.Penalty).ToList();

        penalties.Single().TaxTypeId.ShouldBe(DemoBasic);
        penalties.Single().Amount.ShouldBe(25.50m);
    }

    // --- Traceability and determinism ---

    [Fact]
    public void Calculate_EveryLine_NamesItsRuleAndRate()
    {
        var rate = Rate(DemoBasic, 1m);
        var discount = Prompt(10m);
        var input = Input(100_000m, new(2026, 1, 15), rate) with { DiscountRules = [discount] };

        var result = BillingCalculator.Calculate(input, NoStacking);

        result.Lines.Select(l => (l.RuleId, l.RatePercent)).ShouldBe([(rate.Id, 1m), (discount.Id, 10m)]);
        result.Lines.ShouldAllBe(l => !string.IsNullOrWhiteSpace(l.Explanation));
    }

    [Fact]
    public void Calculate_SameInput_SameResult()
    {
        var input = Input(123_456.78m, new(2026, 6, 15), Rate(DemoBasic, 1m), Rate(DemoSef, 0.5m)) with
        {
            PaymentSchedule = Schedule((3, 31, 25m), (6, 30, 25m), (9, 30, 25m), (12, 31, 25m)),
            DiscountRules = [Prompt(10m)],
            InterestRules = [Interest(2m, 36, InterestMonthCounting.FractionCountsAsFullMonth)],
        };

        var first = BillingCalculator.Calculate(input, NoStacking);
        var second = BillingCalculator.Calculate(input, NoStacking);

        second.Lines.ShouldBe(first.Lines);
        second.Total.ShouldBe(first.Total);
    }

    // --- Refusals ---

    [Fact]
    public void Validate_DraftRule_IsRefused()
    {
        var rate = Rate(DemoBasic, 1m);
        rate.Status = WorkflowStatus.Draft;
        var input = Input(100_000m, new(2026, 1, 15), rate);

        BillingCalculator.Validate(input).ShouldNotBeNull().ShouldContain("approved");
        Should.Throw<InvalidOperationException>(() => BillingCalculator.Calculate(input, NoStacking));
    }

    [Fact]
    public void Validate_NegativeAssessedValue_IsRefused() =>
        BillingCalculator.Validate(Input(-1m, new(2026, 1, 15), Rate(DemoBasic, 1m))).ShouldNotBeNull().ShouldContain("negative");

    [Fact]
    public void Validate_NoApplicableRate_IsRefused() =>
        BillingCalculator.Validate(Input(100_000m, new(2026, 1, 15), Rate(DemoBasic, 1m, DemoCommercial)))
            .ShouldNotBeNull().ShouldContain("no tax rate");

    [Fact]
    public void Validate_TwoRatesSameScope_IsRefused() =>
        BillingCalculator.Validate(Input(100_000m, new(2026, 1, 15), Rate(DemoBasic, 1m), Rate(DemoBasic, 2m)))
            .ShouldNotBeNull().ShouldContain("more than one tax rate");

    [Fact]
    public void Validate_TwoDiscountsSameKindAndScope_IsRefused() =>
        BillingCalculator.Validate(Input(100_000m, new(2026, 1, 15), Rate(DemoBasic, 1m)) with { DiscountRules = [Prompt(10m), Prompt(5m)] })
            .ShouldNotBeNull().ShouldContain("more than one PromptPayment discount rule");

    [Fact]
    public void Validate_SharesNotTotalling100_IsRefused() =>
        BillingCalculator.Validate(Input(100_000m, new(2026, 1, 15), Rate(DemoBasic, 1m)) with { PaymentSchedule = Schedule((3, 31, 60m)) })
            .ShouldNotBeNull().ShouldContain("total 100");

    [Fact]
    public void Validate_FixedPenaltyForAllTaxTypes_IsRefused() =>
        BillingCalculator.Validate(Input(100_000m, new(2026, 1, 15), Rate(DemoBasic, 1m)) with
            {
                PenaltyRules = [Approved(new PenaltyRule { FixedAmount = 10m })],
            })
            .ShouldNotBeNull().ShouldContain("must name a tax type");

    // --- Assessment lines (docs/analysis/mrpaao-forms-model.md §8.4) ---

    private static BillingCalculationInput Lines(DateOnly asOf, TaxRate[] rates, params (Guid Classification, decimal AssessedValue)[] lines) => new()
    {
        AssessedValue = lines.Sum(l => l.AssessedValue),
        ClassificationId = lines[0].Classification,
        Lines = lines.Select(l => new BillingAssessmentLine(l.Classification, l.AssessedValue)).ToList(),
        TaxYear = 2026,
        AsOfDate = asOf,
        TaxRates = rates,
        PaymentSchedule = Annual(),
    };

    [Fact]
    public void Calculate_Lines_EachTaxedAtItsClassificationsRate_AndSummedPerTaxType()
    {
        // DEMO: a general basic rate for every line, and a levy for commercial lines only.
        var input = Lines(new(2026, 1, 15), [Rate(DemoBasic, 1m), Rate(DemoSef, 2m, DemoCommercial)],
            (DemoResidential, 60_000m), (DemoCommercial, 40_000m));

        var result = BillingCalculator.Calculate(input, NoStacking);

        var basic = result.TaxTypes.Single(t => t.TaxTypeId == DemoBasic);
        basic.AssessedValue.ShouldBe(100_000m);
        basic.ComputedAnnualTax.ShouldBe(1_000m);
        basic.Lines.Select(l => (l.ClassificationId, l.Tax)).ShouldBe([(DemoResidential, 600m), (DemoCommercial, 400m)]);
        var levy = result.TaxTypes.Single(t => t.TaxTypeId == DemoSef);
        levy.AssessedValue.ShouldBe(40_000m); // the residential line bears none of it
        levy.AnnualTax.ShouldBe(800m);
        levy.Lines.ShouldHaveSingleItem().ClassificationId.ShouldBe(DemoCommercial);
        result.Total.ShouldBe(1_800m);
    }

    [Fact]
    public void Calculate_Lines_ClassificationSpecificRateWinsPerLine_PrincipalLineNamesTheRate()
    {
        var special = Rate(DemoBasic, 2m, DemoCommercial);
        var input = Lines(new(2026, 1, 15), [Rate(DemoBasic, 1m), special],
            (DemoResidential, 30_000m), (DemoCommercial, 70_000m));

        var basic = BillingCalculator.Calculate(input, NoStacking).TaxTypes.Single();

        basic.ComputedAnnualTax.ShouldBe(300m + 1_400m);
        basic.TaxRateId.ShouldBe(special.Id); // the principal (largest) line's rate
        basic.RatePercent.ShouldBe(2m);
    }

    [Fact]
    public void Calculate_Lines_RoundEachLine_ThenSum()
    {
        var input = Lines(new(2026, 1, 15), [Rate(DemoBasic, 1m)], (DemoResidential, 0.50m), (DemoCommercial, 0.50m));

        // 0.005 rounds away from zero to 0.01 on each line: 0.02, not Money(0.01) = 0.01.
        BillingCalculator.Calculate(input, NoStacking).TaxTypes.Single().AnnualTax.ShouldBe(0.02m);
    }

    [Fact]
    public void Calculate_Lines_CapAppliesToTheTaxTypesTotal()
    {
        var input = Lines(new(2026, 1, 15), [Rate(DemoBasic, 1m)], (DemoResidential, 60_000m), (DemoCommercial, 40_000m)) with
        {
            TaxIncreaseCapRules = [Cap(6m)],
            CapBaselines = [new(DemoBasic, TaxIncreaseCapBaseline.TaxBeforeSmv, 900m)],
        };

        var basic = BillingCalculator.Calculate(input, NoStacking).TaxTypes.Single();

        basic.ComputedAnnualTax.ShouldBe(1_000m);
        basic.AnnualTax.ShouldBe(954m);
    }

    [Fact]
    public void Validate_LinesNotAddingUpToTheAssessedValue_IsRefused() =>
        BillingCalculator.Validate(Lines(new(2026, 1, 15), [Rate(DemoBasic, 1m)], (DemoResidential, 100m)) with { AssessedValue = 101m })
            .ShouldNotBeNull().ShouldContain("differs from the sum of its lines");
}
