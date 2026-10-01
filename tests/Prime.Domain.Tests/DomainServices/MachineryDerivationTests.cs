using Prime.Domain.DomainServices;
using Prime.Domain.Entities;
using Prime.Domain.Enums;
using Shouldly;
using Xunit;

namespace Prime.Domain.Tests.DomainServices;

/// <summary>
/// Step L1-6 (docs/analysis/valuation-foundation.md §4.6, Q11, Q12): years of use, the derived
/// replacement cost, the yearly depreciation cap, and the minimum only while in operation.
/// Every cost, rate and index is DEMO data.
/// </summary>
public class MachineryDerivationTests
{
    private static readonly MachineryValuationParameters Law = new(20m, 5m);

    private static MachineryDerivationInput Input(int years, int life, bool inOperation = true, decimal? fxA = null, decimal? fxV = null) =>
        new(1_000_000m, 0m, fxA, fxV, 100m, 100m, years, life, inOperation);

    [Theory]
    [InlineData("2020-03-01", "2026-01-01", 5)]
    [InlineData("2020-01-01", "2026-01-01", 6)]  // the anniversary counts
    [InlineData("2020-01-02", "2026-01-01", 5)]
    [InlineData("2026-06-01", "2026-01-01", 0)]  // installed after the valuation date
    public void YearsBetween_CountsCompletedYears(string start, string date, int expected) =>
        ValuationCalculator.YearsBetween(DateOnly.Parse(start), DateOnly.Parse(date)).ShouldBe(expected);

    [Fact]
    public void ReplacementCost_ConvertsAndTrendsTheCostInsuranceFreight_AndAddsTheOtherExpenses()
    {
        var result = ValuationCalculator.CalculateMachineryDerived(new MachineryDerivationInput(1_060_000m, 40_000m, 50m, 60m, 100m, 110m, 0, 10, true), Law);

        result.Method.ShouldBe(ValuationMethod.DerivedReplacementCost);
        result.Breakdown["ReplacementCost"].ShouldBe(1_439_200m);
        result.MarketValue.ShouldBe(1_439_200m); // no years of use: no depreciation
    }

    [Theory]
    [InlineData(4, 20, 20, 0)]   // 4/20 = 20 % = 5 % × 4: equal, not capped
    [InlineData(4, 10, 20, 1)]   // 4/10 = 40 %, capped at 20 %
    [InlineData(4, 40, 10, 0)]   // 4/40 = 10 %, under the cap
    public void Depreciation_IsTheSmallerOfTheLifeRatioAndFivePercentAYear(int years, int life, double percent, int capped)
    {
        var b = ValuationCalculator.CalculateMachineryDerived(Input(years, life), Law).Breakdown;

        b["DepreciationPercent"].ShouldBe((decimal)percent);
        b["DepreciationCapped"].ShouldBe((decimal)capped);
    }

    [Fact]
    public void TheMinimum_HoldsWhileInOperation_AndDepreciationNeverExceedsTheCost()
    {
        ValuationCalculator.CalculateMachineryDerived(Input(18, 10), Law).MarketValue.ShouldBe(200_000m);            // 10 % left → 20 %
        ValuationCalculator.CalculateMachineryDerived(Input(18, 10, inOperation: false), Law).MarketValue.ShouldBe(100_000m);
        ValuationCalculator.CalculateMachineryDerived(Input(30, 10, inOperation: false), Law).MarketValue.ShouldBe(0m);  // capped at 100 %
    }

    [Fact]
    public void WithoutTheYearlyLimitConfigured_TheDerivedMethodRefuses() =>
        Should.Throw<InvalidOperationException>(() => ValuationCalculator.CalculateMachineryDerived(Input(1, 10), new MachineryValuationParameters(20m)));

    [Fact]
    public void EnteredMethod_HasNoMinimum_ForMachineryNotInOperation()
    {
        var machine = new Machinery { ReplacementCost = 1_000_000m, EconomicLifeYears = 10, RemainingLifeYears = 1, IsInOperation = false };

        ValuationCalculator.CalculateMachinery(machine, Law).MarketValue.ShouldBe(100_000m);
        machine.IsInOperation = true;
        ValuationCalculator.CalculateMachinery(machine, Law).MarketValue.ShouldBe(200_000m);
    }

    [Fact]
    public void BrandNew_AddsItsCostItems()
    {
        var machine = new Machinery
        {
            IsBrandNew = true, AcquisitionCost = 500_000m, InstallationCost = 10_000m,
            CostItems = [new MachineryCostItem { Kind = MachineryCostItemKind.Freight, Amount = 20_000m }],
        };

        ValuationCalculator.CalculateMachinery(machine, Law).MarketValue.ShouldBe(530_000m);
    }
}
