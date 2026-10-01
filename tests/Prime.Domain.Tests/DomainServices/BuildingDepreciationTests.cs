using Prime.Domain.DomainServices;
using Prime.Domain.Entities;
using Prime.Domain.Enums;
using Shouldly;
using Xunit;

namespace Prime.Domain.Tests.DomainServices;

/// <summary>
/// Step L1-5 (docs/analysis/valuation-foundation.md §4.5, Q8, Q9): building age, the two ways a
/// depreciation table reads, the remaining-value cap, and the construction-cost calculation.
/// Every band and percent is DEMO data, not an SMV's.
/// </summary>
public class BuildingDepreciationTests
{
    private static readonly DateOnly Jan2026 = new(2026, 1, 1);

    private static List<SmvDepreciationRow> Rows(params (int From, int? To, decimal Percent)[] bands) =>
        bands.Select((b, i) => new SmvDepreciationRow { Sequence = i + 1, FromAge = b.From, ToAge = b.To, Percent = b.Percent }).ToList();

    [Fact]
    public void Age_ReadsCompleted_ElseConstructed_ElseOccupied_NeverBelowZero()
    {
        BuildingDepreciation.Age(new Building { YearCompleted = 2016, YearConstructed = 2014 }, Jan2026).ShouldBe(10);
        BuildingDepreciation.Age(new Building { YearConstructed = 2014 }, Jan2026).ShouldBe(12);
        BuildingDepreciation.Age(new Building { DateOccupied = new DateOnly(2020, 6, 1) }, Jan2026).ShouldBe(6);
        BuildingDepreciation.Age(new Building { YearCompleted = 2027 }, Jan2026).ShouldBe(0);
        BuildingDepreciation.Age(new Building(), Jan2026).ShouldBeNull();
    }

    [Theory]
    [InlineData(0, 0)]     // below the first band
    [InlineData(1, 10)]
    [InlineData(5, 10)]    // band edge, inclusive
    [InlineData(6, 25)]
    [InlineData(40, 60)]   // open last band
    public void Cumulative_TakesTheBandHoldingTheAge(int age, double expected)
    {
        var outcome = BuildingDepreciation.Percent(DepreciationReading.Cumulative, Rows((1, 5, 10m), (6, 10, 25m), (11, null, 60m)), 0m, age);

        outcome.Problem.ShouldBeNull();
        outcome.Percent.ShouldBe((decimal)expected);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(3, 6)]     // 3 × 2
    [InlineData(5, 10)]    // 5 × 2
    [InlineData(6, 13)]    // 5 × 2 + 1 × 3
    [InlineData(10, 25)]   // 5 × 2 + 5 × 3
    public void YearlyWithinBand_AddsEachBandsYears(int age, double expected)
    {
        var outcome = BuildingDepreciation.Percent(DepreciationReading.YearlyWithinBand, Rows((1, 5, 2m), (6, null, 3m)), 0m, age);

        outcome.Percent.ShouldBe((decimal)expected);
    }

    [Fact]
    public void TheRemainingValue_CapsTheDepreciation()
    {
        var capped = BuildingDepreciation.Percent(DepreciationReading.YearlyWithinBand, Rows((1, null, 3m)), 20m, 30); // 90 % → 80 %
        (capped.Percent, capped.Capped).ShouldBe((80m, true));
        var exact = BuildingDepreciation.Percent(DepreciationReading.YearlyWithinBand, Rows((1, null, 4m)), 20m, 20);  // exactly 80 %
        (exact.Percent, exact.Capped).ShouldBe((80m, false));
    }

    [Fact]
    public void AnAgeBeyondTheTable_IsRefused()
    {
        BuildingDepreciation.Percent(DepreciationReading.Cumulative, Rows((0, 10, 20m)), 20m, 11).Problem.ShouldNotBeNull();
        BuildingDepreciation.Percent(DepreciationReading.Cumulative, Rows(), 20m, 1).Problem.ShouldNotBeNull();
    }

    [Fact]
    public void BandsProblem_RequiresContiguousBands_OnlyTheLastOpen()
    {
        BuildingDepreciation.BandsProblem([(0, 4, 0m), (5, 9, 10m), (10, null, 20m)]).ShouldBeNull();
        BuildingDepreciation.BandsProblem([(10, null, 20m), (0, 9, 10m)]).ShouldBeNull(); // any order
        BuildingDepreciation.BandsProblem([]).ShouldNotBeNull();
        BuildingDepreciation.BandsProblem([(0, 4, 0m), (6, null, 10m)]).ShouldNotBeNull();  // gap
        BuildingDepreciation.BandsProblem([(0, 5, 0m), (5, null, 10m)]).ShouldNotBeNull();  // overlap
        BuildingDepreciation.BandsProblem([(0, null, 0m), (5, 9, 10m)]).ShouldNotBeNull();  // open band not last
        BuildingDepreciation.BandsProblem([(3, 2, 0m)]).ShouldNotBeNull();
        BuildingDepreciation.BandsProblem([(0, null, 101m)]).ShouldNotBeNull();
    }

    [Fact]
    public void CalculateBuildingByCost_LaysOutTheFaasFigures()
    {
        var result = ValuationCalculator.CalculateBuildingByCost(100m, 10_000m, [("FENCE", 20_000m), ("FENCE", 10_000m), ("GATE", 5_000m)], 80m,
            new BuildingDepreciationInput(25m, 10, false, false));

        result.Breakdown["BaseValue"].ShouldBe(1_000_000m);
        result.Breakdown["AdditionalItemsCost"].ShouldBe(35_000m);
        result.Breakdown["ExtraItem:FENCE"].ShouldBe(30_000m);
        result.Breakdown["ExtraItem:GATE"].ShouldBe(5_000m);
        result.Breakdown["TotalConstructionCost"].ShouldBe(828_000m); // 1,035,000 × 80 %
        result.Breakdown["Depreciation"].ShouldBe(207_000m);
        result.Breakdown["Age"].ShouldBe(10m);
        result.MarketValue.ShouldBe(621_000m);

        var carried = ValuationCalculator.CalculateBuildingByCost(10m, 1_000m, [], 100m, new BuildingDepreciationInput(25m, null, true, false));
        carried.Breakdown.ContainsKey("Age").ShouldBeFalse();
        carried.Breakdown["DepreciationCarriedOver"].ShouldBe(1m);
        carried.MarketValue.ShouldBe(7_500m);
    }
}
