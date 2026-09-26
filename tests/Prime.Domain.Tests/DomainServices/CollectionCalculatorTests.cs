using Prime.Domain.DomainServices;
using Prime.Domain.Entities.Billing;
using Prime.Domain.Enums;
using Shouldly;
using Xunit;

namespace Prime.Domain.Tests.DomainServices;

/// <summary>
/// CLAUDE.md §75 financial cases for <see cref="CollectionCalculator"/>
/// (docs/analysis/collection.md §2). Every rate, date and amount is DEMO
/// test data (CLAUDE.md §81), chosen to make the arithmetic easy to check —
/// none is a real ordinance value.
/// </summary>
public class CollectionCalculatorTests
{
    private static readonly Guid DemoBasic = Guid.NewGuid();
    private static readonly Guid DemoSef = Guid.NewGuid();
    private static readonly Guid DemoRpu = Guid.NewGuid();
    private static readonly Guid DemoRate = Guid.NewGuid();

    private static T Approved<T>(T rule) where T : BillingRule
    {
        rule.LegalBasis = "DEMO";
        rule.OrdinanceNumber = "DEMO-ORD";
        rule.EffectiveDate = new DateOnly(2020, 1, 1);
        rule.Status = WorkflowStatus.Approved;
        return rule;
    }

    private static readonly DiscountRule Prompt = Approved(new DiscountRule { Kind = DiscountKind.PromptPayment, Rate = 10m });
    private static readonly DiscountRule Advance = Approved(new DiscountRule
    {
        Kind = DiscountKind.AdvancePayment, Rate = 20m, CutoffMonth = 12, CutoffDay = 31, CutoffYearOffset = -1,
    });
    private static readonly PenaltyRule PenaltyPercent = Approved(new PenaltyRule { Rate = 5m, AppliesAfterDays = 0 });
    private static readonly PenaltyRule PenaltyFixed = Approved(new PenaltyRule { TaxTypeId = DemoBasic, FixedAmount = 50m });
    private static readonly InterestRule Interest2 = Approved(new InterestRule
    {
        RatePerMonth = 2m, MaxMonths = 36, MonthCounting = InterestMonthCounting.CompletedMonthsOnly,
    });

    private static CollectionKey Key(int seq, Guid taxType, int year, int month, int day, decimal owed, decimal paid = 0m, bool fixedCharged = false) =>
        new(seq, taxType, DemoRate, new DateOnly(year, month, day), owed, paid, fixedCharged);

    private static CollectionBill Bill(int year, IReadOnlyList<CollectionKey> keys,
        DiscountRule[]? discounts = null, PenaltyRule[]? penalties = null, InterestRule[]? interest = null, bool stacking = false) =>
        new(Guid.NewGuid(), DemoRpu, year, stacking, discounts ?? [], penalties ?? [], interest ?? [], keys);

    /// <summary>A DEMO year of four quarterly installments of 250 (basic) each.</summary>
    private static List<CollectionKey> Quarters(int year, decimal each = 250m) =>
    [
        Key(1, DemoBasic, year, 3, 31, each), Key(2, DemoBasic, year, 6, 30, each),
        Key(3, DemoBasic, year, 9, 30, each), Key(4, DemoBasic, year, 12, 31, each),
    ];

    private static CollectionItem Whole(int year, int seq) => new(DemoRpu, year, seq, null);

    private static CollectionResult Allocate(DateOnly on, IReadOnlyList<CollectionBill> bills, params CollectionItem[] items) =>
        CollectionCalculator.Allocate(new CollectionInput(on, bills, items));

    private static decimal Sum(CollectionResult r, BillingComponent c) =>
        r.Allocations.Where(a => a.Component == c).Sum(a => a.Amount);

    // --- Full payment ---

    [Fact]
    public void WholeInstallment_OnTimeWithoutRules_IsItsPrincipal()
    {
        var result = Allocate(new(2026, 3, 1), [Bill(2026, Quarters(2026))], Whole(2026, 1));

        result.Total.ShouldBe(250m);
        result.Allocations.Single().Component.ShouldBe(BillingComponent.Tax);
        result.Allocations.Single().YearCategory.ShouldBe(CollectionYearCategory.Current);
    }

    [Fact]
    public void WholeInstallment_SettlesEveryTaxTypeOfIt()
    {
        var bill = Bill(2026, [Key(1, DemoBasic, 2026, 3, 31, 1_000m), Key(1, DemoSef, 2026, 3, 31, 500m)]);

        var result = Allocate(new(2026, 3, 1), [bill], Whole(2026, 1));

        result.Allocations.Select(a => (a.TaxTypeId, a.Amount)).ShouldBe([(DemoBasic, 1_000m), (DemoSef, 500m)]);
        result.Principal.ShouldBe(1_500m);
    }

    [Fact]
    public void LargeAmounts_StayExact()
    {
        var bill = Bill(2026, [Key(1, DemoBasic, 2026, 3, 31, 987_654_321.99m)], penalties: [PenaltyPercent], interest: [Interest2]);

        var result = Allocate(new(2026, 6, 30), [bill], Whole(2026, 1));

        // 5% = 49,382,716.0995 → .10; 2% × 3 completed months (31 Mar → 30 Jun) = 59,259,259.3194 → .32
        Sum(result, BillingComponent.Penalty).ShouldBe(49_382_716.10m);
        Sum(result, BillingComponent.Interest).ShouldBe(59_259_259.32m);
        result.Total.ShouldBe(987_654_321.99m + 49_382_716.10m + 59_259_259.32m);
    }

    // --- Discounts ---

    [Fact]
    public void PromptDiscount_WhenTheWholeKeyIsPaidOnTime()
    {
        var result = Allocate(new(2026, 3, 31), [Bill(2026, Quarters(2026), discounts: [Prompt])], Whole(2026, 1));

        Sum(result, BillingComponent.Discount).ShouldBe(-25m);
        result.Total.ShouldBe(225m);
    }

    [Fact]
    public void NoDiscount_OnPartOfAnInstallment()
    {
        var result = Allocate(new(2026, 3, 1), [Bill(2026, Quarters(2026), discounts: [Prompt])],
            new CollectionItem(DemoRpu, 2026, 1, 100m));

        result.Allocations.ShouldHaveSingleItem().Amount.ShouldBe(100m);
    }

    [Fact]
    public void NoDiscount_OnTheRemainderOfAKeyPartlyPaidBefore()
    {
        var keys = new List<CollectionKey> { Key(1, DemoBasic, 2026, 3, 31, 250m, paid: 100m) };

        var result = Allocate(new(2026, 3, 1), [Bill(2026, keys, discounts: [Prompt])], Whole(2026, 1));

        result.Total.ShouldBe(150m);
        Sum(result, BillingComponent.Discount).ShouldBe(0m);
    }

    [Fact]
    public void AdvanceDiscount_WhenTheWholeYearIsPaidInOnePaymentBeforeTheCutoff()
    {
        var bill = Bill(2027, Quarters(2027), discounts: [Advance]);

        var result = Allocate(new(2026, 12, 15), [bill], Whole(2027, 1), Whole(2027, 2), Whole(2027, 3), Whole(2027, 4));

        Sum(result, BillingComponent.Discount).ShouldBe(-200m);
        result.Total.ShouldBe(800m);
        result.Allocations.ShouldAllBe(a => a.YearCategory == CollectionYearCategory.Advance);
    }

    [Fact]
    public void NoAdvanceDiscount_WhenOnlyPartOfTheYearIsPaid()
    {
        var result = Allocate(new(2026, 12, 15), [Bill(2027, Quarters(2027), discounts: [Advance])], Whole(2027, 1));

        Sum(result, BillingComponent.Discount).ShouldBe(0m);
    }

    [Fact]
    public void BothDiscounts_WithoutStacking_OnlyTheLarger()
    {
        var bill = Bill(2027, Quarters(2027), discounts: [Prompt, Advance]);

        var result = Allocate(new(2026, 12, 15), [bill], Whole(2027, 1), Whole(2027, 2), Whole(2027, 3), Whole(2027, 4));

        result.Allocations.Where(a => a.Component == BillingComponent.Discount).ShouldAllBe(a => a.RatePercent == 20m);
        Sum(result, BillingComponent.Discount).ShouldBe(-200m);
    }

    // --- Penalty and interest ---

    [Fact]
    public void LatePayment_ChargesPenaltyAndInterestOnThePrincipalPaid()
    {
        var bill = Bill(2026, Quarters(2026), penalties: [PenaltyPercent], interest: [Interest2]);

        var result = Allocate(new(2026, 7, 31), [bill], Whole(2026, 1));

        Sum(result, BillingComponent.Penalty).ShouldBe(12.50m);          // 5% of 250
        Sum(result, BillingComponent.Interest).ShouldBe(20m);            // 2% × 4 months of 250
        result.Allocations.Single(a => a.Component == BillingComponent.Interest).Months.ShouldBe(4);
        result.Total.ShouldBe(282.50m);
    }

    [Fact]
    public void LatePartialPayment_ChargesOnlyThePartPaid()
    {
        var bill = Bill(2026, Quarters(2026), penalties: [PenaltyPercent], interest: [Interest2]);

        var result = Allocate(new(2026, 7, 31), [bill], new CollectionItem(DemoRpu, 2026, 1, 100m));

        Sum(result, BillingComponent.Tax).ShouldBe(100m);
        Sum(result, BillingComponent.Penalty).ShouldBe(5m);
        Sum(result, BillingComponent.Interest).ShouldBe(8m);
    }

    [Fact]
    public void RemainderPaidLater_AccruesItsOwnInterestOnly()
    {
        // 100 of 250 was paid earlier; the remaining 150 is paid 6 months after the due date.
        var keys = new List<CollectionKey> { Key(1, DemoBasic, 2026, 3, 31, 250m, paid: 100m) };

        var result = Allocate(new(2026, 9, 30), [Bill(2026, keys, interest: [Interest2])], Whole(2026, 1));

        Sum(result, BillingComponent.Interest).ShouldBe(18m);            // 2% × 6 × 150
        result.Total.ShouldBe(168m);
    }

    [Fact]
    public void FixedPenalty_IsChargedOncePerKey()
    {
        var first = Allocate(new(2026, 5, 1), [Bill(2026, Quarters(2026), penalties: [PenaltyFixed])],
            new CollectionItem(DemoRpu, 2026, 1, 100m));
        var keys = new List<CollectionKey> { Key(1, DemoBasic, 2026, 3, 31, 250m, paid: 100m, fixedCharged: true) };
        var second = Allocate(new(2026, 6, 1), [Bill(2026, keys, penalties: [PenaltyFixed])], Whole(2026, 1));

        Sum(first, BillingComponent.Penalty).ShouldBe(50m);
        Sum(second, BillingComponent.Penalty).ShouldBe(0m);
    }

    [Fact]
    public void InterestMonths_AreCappedByTheRule()
    {
        var bill = Bill(2020, [Key(1, DemoBasic, 2020, 3, 31, 1_000m)], interest: [Interest2]);

        var result = Allocate(new(2026, 3, 31), [bill], Whole(2020, 1));

        result.Allocations.Single(a => a.Component == BillingComponent.Interest).Months.ShouldBe(36);
        Sum(result, BillingComponent.Interest).ShouldBe(720m);
        result.Allocations.ShouldAllBe(a => a.YearCategory == CollectionYearCategory.Prior);
    }

    // --- Partial payments and rounding ---

    [Fact]
    public void Partial_IsSplitAcrossTaxTypesInProportion()
    {
        var bill = Bill(2026, [Key(1, DemoBasic, 2026, 3, 31, 1_000m), Key(1, DemoSef, 2026, 3, 31, 500m)]);

        var result = Allocate(new(2026, 3, 1), [bill], new CollectionItem(DemoRpu, 2026, 1, 300m));

        result.Allocations.Select(a => (a.TaxTypeId, a.Amount)).ShouldBe([(DemoBasic, 200m), (DemoSef, 100m)]);
    }

    [Fact]
    public void Partial_RoundingRemainderKeepsTheTotalExact()
    {
        var bill = Bill(2026, [
            Key(1, DemoBasic, 2026, 3, 31, 100m), Key(1, DemoSef, 2026, 3, 31, 100m), Key(1, Guid.NewGuid(), 2026, 3, 31, 100m),
        ]);

        var result = Allocate(new(2026, 3, 1), [bill], new CollectionItem(DemoRpu, 2026, 1, 100m));

        // 33.33 each, the remaining centavo to the first in bill order.
        result.Allocations.Select(a => a.Amount).ShouldBe([33.34m, 33.33m, 33.33m]);
        result.Total.ShouldBe(100m);
    }

    [Fact]
    public void Partial_EqualToEverythingOwed_IsAWholePayment_AndGetsItsDiscount()
    {
        var result = Allocate(new(2026, 3, 1), [Bill(2026, Quarters(2026), discounts: [Prompt])],
            new CollectionItem(DemoRpu, 2026, 1, 250m));

        result.Total.ShouldBe(225m);
    }

    [Fact]
    public void DecimalCentavos_AreKept()
    {
        var keys = new List<CollectionKey> { Key(1, DemoBasic, 2026, 3, 31, 333.33m) };

        var result = Allocate(new(2026, 3, 1), [Bill(2026, keys, discounts: [Prompt])], Whole(2026, 1));

        Sum(result, BillingComponent.Discount).ShouldBe(-33.33m);
        result.Total.ShouldBe(300m);
    }

    // --- Multiple years and ordering ---

    [Fact]
    public void MultiYear_OldestYearFirst_EachWithItsOwnRulesAndCategory()
    {
        var bill2026 = Bill(2026, [Key(1, DemoBasic, 2026, 3, 31, 1_000m)], discounts: [Prompt]);
        var bill2025 = Bill(2025, [Key(1, DemoBasic, 2025, 3, 31, 1_000m)], penalties: [PenaltyPercent]);

        var result = Allocate(new(2026, 3, 1), [bill2026, bill2025], Whole(2026, 1), Whole(2025, 1));

        result.Allocations.Select(a => (a.TaxYear, a.Component, a.Amount)).ShouldBe([
            (2025, BillingComponent.Tax, 1_000m), (2025, BillingComponent.Penalty, 50m),
            (2026, BillingComponent.Tax, 1_000m), (2026, BillingComponent.Discount, -100m),
        ]);
        result.Allocations.Where(a => a.TaxYear == 2025).ShouldAllBe(a => a.YearCategory == CollectionYearCategory.Prior);
        result.Total.ShouldBe(1_950m);
    }

    [Fact]
    public void AllOutstanding_ListsUnpaidInstallmentsOldestFirst()
    {
        var bills = new List<CollectionBill>
        {
            Bill(2026, [Key(1, DemoBasic, 2026, 3, 31, 100m), Key(2, DemoBasic, 2026, 6, 30, 100m, paid: 100m)]),
            Bill(2025, [Key(1, DemoBasic, 2025, 3, 31, 100m)]),
        };

        CollectionCalculator.AllOutstanding(bills).Select(i => (i.TaxYear, i.InstallmentSequence)).ShouldBe([(2025, 1), (2026, 1)]);
    }

    // --- Refusals ---

    [Fact]
    public void Refuses_AnInstallmentAlreadyPaid()
    {
        var keys = new List<CollectionKey> { Key(1, DemoBasic, 2026, 3, 31, 250m, paid: 250m) };

        CollectionCalculator.Validate(new(new(2026, 3, 1), [Bill(2026, keys)], [Whole(2026, 1)]))!
            .Kind.ShouldBe(CollectionProblemKind.AlreadySettled);
    }

    [Fact]
    public void Refuses_AZeroPrincipalInstallment_AsAlreadySettled()
    {
        var keys = new List<CollectionKey> { Key(1, DemoBasic, 2026, 3, 31, 0m) };

        CollectionCalculator.Validate(new(new(2026, 3, 1), [Bill(2026, keys)], [Whole(2026, 1)]))!
            .Kind.ShouldBe(CollectionProblemKind.AlreadySettled);
    }

    [Fact]
    public void Refuses_AKeyPaidBeyondALoweredBill_AsAlreadySettled()
    {
        var keys = new List<CollectionKey> { Key(1, DemoBasic, 2026, 3, 31, 200m, paid: 250m) };

        keys[0].Outstanding.ShouldBe(0m);
        CollectionCalculator.Validate(new(new(2026, 3, 1), [Bill(2026, keys)], [Whole(2026, 1)]))!
            .Kind.ShouldBe(CollectionProblemKind.AlreadySettled);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(10.005)]
    [InlineData(250.01)]
    public void Refuses_AnInvalidPartialAmount(decimal amount)
    {
        CollectionCalculator.Validate(new(new(2026, 3, 1), [Bill(2026, Quarters(2026))], [new CollectionItem(DemoRpu, 2026, 1, amount)]))!
            .Kind.ShouldBe(CollectionProblemKind.InvalidAmount);
    }

    [Fact]
    public void Refuses_AnInstallmentWithNoPostedBill()
    {
        CollectionCalculator.Validate(new(new(2026, 3, 1), [Bill(2026, Quarters(2026))], [Whole(2026, 5)]))!
            .Kind.ShouldBe(CollectionProblemKind.UnknownInstallment);
    }

    [Fact]
    public void Refuses_TheSameInstallmentTwice_AndAnEmptySelection()
    {
        var bills = new List<CollectionBill> { Bill(2026, Quarters(2026)) };

        CollectionCalculator.Validate(new(new(2026, 3, 1), bills, [Whole(2026, 1), Whole(2026, 1)]))!
            .Kind.ShouldBe(CollectionProblemKind.DuplicateItem);
        CollectionCalculator.Validate(new(new(2026, 3, 1), bills, []))!
            .Kind.ShouldBe(CollectionProblemKind.NothingSelected);
        Should.Throw<InvalidOperationException>(() => Allocate(new(2026, 3, 1), bills));
    }

    // --- Traceability and determinism ---

    [Fact]
    public void EveryChargeNamesItsRuleAndRate()
    {
        var bill = Bill(2026, Quarters(2026), penalties: [PenaltyPercent], interest: [Interest2]);

        var result = Allocate(new(2026, 7, 31), [bill], Whole(2026, 1));

        result.Allocations.Single(a => a.Component == BillingComponent.Penalty).RuleId.ShouldBe(PenaltyPercent.Id);
        result.Allocations.Single(a => a.Component == BillingComponent.Interest).RatePercent.ShouldBe(2m);
        result.Allocations.Single(a => a.Component == BillingComponent.Tax).RuleId.ShouldBe(DemoRate);
        result.Allocations.ShouldAllBe(a => a.BillId == bill.BillId && a.Explanation.Length > 0);
    }

    [Fact]
    public void SameInput_SameResult()
    {
        var bills = new List<CollectionBill> { Bill(2026, Quarters(2026), discounts: [Prompt], penalties: [PenaltyPercent], interest: [Interest2]) };
        var input = new CollectionInput(new(2026, 7, 31), bills, [Whole(2026, 1), new CollectionItem(DemoRpu, 2026, 3, 77.77m)]);

        CollectionCalculator.Allocate(input).Allocations.ShouldBe(CollectionCalculator.Allocate(input).Allocations);
    }
}
