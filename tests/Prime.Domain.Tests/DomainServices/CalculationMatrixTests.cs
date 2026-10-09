using Prime.Domain.DomainServices;
using Prime.Domain.Entities;
using Prime.Domain.Enums;
using Prime.Domain.Exceptions;
using Shouldly;
using Xunit;

namespace Prime.Domain.Tests.DomainServices;

/// <summary>
/// Step H3 (docs/analysis/production-hardening.md §4.6; CLAUDE.md §75): the calculation matrix's domain cells not
/// covered elsewhere: rounding to the centavo, zero, the largest storable amount and beyond, fractional inputs, the
/// edge of a depreciation table, and repeatability. The full matrix and where each cell is tested: docs/TESTING.md.
/// Every rate, cost and percent is DEMO data.
/// </summary>
public class CalculationMatrixTests
{
    private static SmvSchedule Schedule(decimal rate, decimal? min = null, decimal? max = null) =>
        new() { MarketValue = rate, MinimumValue = min, MaximumValue = max };

    private static ValuationCalculationResult Result(decimal value) =>
        new(ValuationMethod.SmvBased, value, new Dictionary<string, decimal> { ["MarketValue"] = value });

    // ---- Rounding -------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("0.005", "0.01")]       // half: away from zero
    [InlineData("0.004999", "0.00")]
    [InlineData("2.675", "2.68")]       // banker's rounding would give 2.68 too; the next one differs
    [InlineData("2.665", "2.67")]       // half to even would give 2.66
    [InlineData("-0.005", "-0.01")]
    [InlineData("1000000.003931", "1000000.00")]
    [InlineData("9999999999999999.99", "9999999999999999.99")]
    public void Money_RoundsToTheCentavo_HalfAwayFromZero(string value, string expected) =>
        Money.ToCentavo(decimal.Parse(value)).ShouldBe(decimal.Parse(expected));

    [Fact]
    public void ARowRoundedToTheCentavo_KeepsTheValueBeforeRounding()
    {
        var rounded = ValuationCalculator.ToCentavo(Result(1_000_000.003931m));

        rounded.MarketValue.ShouldBe(1_000_000.00m);
        rounded.Breakdown["MarketValue"].ShouldBe(1_000_000.00m);
        rounded.Breakdown["MarketValueBeforeRounding"].ShouldBe(1_000_000.003931m);
    }

    [Fact]
    public void ARowAlreadyInCentavos_IsUnchanged_AndRecordsNoRounding()
    {
        var row = Result(500_000.25m);

        ValuationCalculator.ToCentavo(row).ShouldBeSameAs(row);
    }

    [Fact]
    public void AfterAConfiguredStep_TheValueBeforeTheStepIsTheOneKept()
    {
        var stepped = ValuationCalculator.WithRounding(Result(1_234.5678m), 0.001m); // a step finer than a centavo
        var rounded = ValuationCalculator.ToCentavo(stepped);

        rounded.MarketValue.ShouldBe(1_234.57m);
        rounded.Breakdown["MarketValueBeforeRounding"].ShouldBe(1_234.5678m);
        rounded.Breakdown["RoundingStep"].ShouldBe(0.001m);
    }

    // ---- Zero -----------------------------------------------------------------------------------------------------

    [Fact]
    public void ZeroInputs_GiveZero_NotAnError()
    {
        ValuationCalculator.CalculateLand(new Land { Area = 1_000m }, Schedule(0m)).MarketValue.ShouldBe(0m);
        ValuationCalculator.CalculateImprovement(0m, Schedule(150m)).MarketValue.ShouldBe(0m);
        ValuationCalculator.CalculateBuildingByCost(0m, 12_000m, [], 100m, new BuildingDepreciationInput(10m, 5, false, false)).MarketValue.ShouldBe(0m);
        ValuationCalculator.CalculateMachinery(new Machinery { IsBrandNew = true, AcquisitionCost = 0m }, new MachineryValuationParameters(20m))
            .MarketValue.ShouldBe(0m);
        ValuationCalculator.CalculateMachineryDerived(new MachineryDerivationInput(0m, 0m, null, null, 100m, 120m, 3, 10, true),
            new MachineryValuationParameters(20m, 5m)).MarketValue.ShouldBe(0m);
    }

    [Fact]
    public void Adjustments_OfExactlyMinusOneHundredPercent_GiveZero()
    {
        var strip = ValuationCalculator.CalculateLandStrip(500m, Schedule(1_000m), null,
            [new LandAdjustmentInput("A", "DEMO A", -60m), new LandAdjustmentInput("B", "DEMO B", -40m)]);

        strip.MarketValue.ShouldBe(0m);
    }

    // ---- Large amounts --------------------------------------------------------------------------------------------

    [Fact]
    public void TheLargestArea_TimesALargeRate_IsExact_AndStorable()
    {
        // numeric(14,4) area × numeric(18,2) rate: decimal keeps all 22 digits.
        var land = ValuationCalculator.CalculateLand(new Land { Area = 9_999_999_999.9999m }, Schedule(999_999.99m));

        land.MarketValue.ShouldBe(9_999_999_899_999_900.000001m);
        ValuationCalculator.ToCentavo(land).MarketValue.ShouldBe(9_999_999_899_999_900.00m);
    }

    [Fact]
    public void AValueBeyondWhatCanBeStored_IsRefused()
    {
        Money.Checked(Money.MaxAmount, "x").ShouldBe(Money.MaxAmount);
        Should.Throw<ValueOutOfRangeException>(() => Money.Checked(Money.MaxAmount + 0.01m, "x")).Code.ShouldBe("VALUE_OUT_OF_RANGE");

        var land = ValuationCalculator.CalculateLand(new Land { Area = 9_999_999_999.9999m }, Schedule(2_000_000m)); // about 2 × 10^16
        Should.Throw<ValueOutOfRangeException>(() => ValuationCalculator.ToCentavo(land));
    }

    [Fact]
    public void AValueBeyondDecimal_Overflows_WhichTheApiReportsAsOutOfRange()
    {
        // Mapped to 400 VALUE_OUT_OF_RANGE by ExceptionHandlingMiddleware (Prime.IntegrationTests CalculationMatrixTests).
        Should.Throw<OverflowException>(() =>
            ValuationCalculator.CalculateLand(new Land { Area = 9_999_999_999.9999m, LocationFactor = 999.999999m }, Schedule(9_999_999_999_999_999.99m)));
    }

    // ---- Fractional inputs ----------------------------------------------------------------------------------------

    [Fact]
    public void FractionalAreaRateAndAdjustment_KeepFullPrecisionUntilTheCentavo()
    {
        var strip = ValuationCalculator.CalculateLandStrip(123.4567m, Schedule(1_000.01m), null, [new LandAdjustmentInput("A", "DEMO A", -7.5m)]);

        strip.Breakdown["BaseValue"].ShouldBe(123_457.934567m);
        strip.MarketValue.ShouldBe(114_198.589474475m);
        ValuationCalculator.ToCentavo(strip).MarketValue.ShouldBe(114_198.59m);
    }

    [Fact]
    public void ARepeatingFraction_RoundsTheSameWayEveryTime()
    {
        var machine = new Machinery { ReplacementCost = 100_000m, EconomicLifeYears = 3, RemainingLifeYears = 1, IsInOperation = true };
        var parameters = new MachineryValuationParameters(20m);

        var first = ValuationCalculator.ToCentavo(ValuationCalculator.CalculateMachinery(machine, parameters));
        var second = ValuationCalculator.ToCentavo(ValuationCalculator.CalculateMachinery(machine, parameters));

        first.MarketValue.ShouldBe(33_333.33m);
        first.Breakdown.ShouldBe(second.Breakdown);
    }

    // ---- Depreciation limits --------------------------------------------------------------------------------------

    [Fact]
    public void TheLastAgeOfABoundedTable_IsDepreciated_TheNextIsRefused()
    {
        List<SmvDepreciationRow> rows = [new() { Sequence = 1, FromAge = 1, ToAge = 10, Percent = 2m }];

        var last = BuildingDepreciation.Percent(DepreciationReading.YearlyWithinBand, rows, 20m, 10);
        (last.Problem, last.Percent).ShouldBe((null, 20m));
        BuildingDepreciation.Percent(DepreciationReading.YearlyWithinBand, rows, 20m, 11).Problem.ShouldNotBeNull();
    }

    [Fact]
    public void BuildingDepreciation_OfAFractionalCost_IsTakenFromTheTotalBeforeRounding()
    {
        // 87.5 sqm × 13,333.33 = 1,166,666.375; 37 % of it = 431,666.55875; the rest 734,999.81625.
        var building = ValuationCalculator.CalculateBuildingByCost(87.5m, 13_333.33m, [], 100m, new BuildingDepreciationInput(37m, 12, false, false));

        building.Breakdown["Depreciation"].ShouldBe(431_666.55875m);
        ValuationCalculator.ToCentavo(building).MarketValue.ShouldBe(734_999.82m);
    }
}
