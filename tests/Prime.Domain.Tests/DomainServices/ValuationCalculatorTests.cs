using Prime.Domain.DomainServices;
using Prime.Domain.Entities;
using Prime.Domain.Enums;
using Shouldly;
using Xunit;

namespace Prime.Domain.Tests.DomainServices;

public class ValuationCalculatorTests
{
    private static SmvSchedule Schedule(decimal rate, decimal? min = null, decimal? max = null) => new()
    {
        MarketValue = rate,
        MinimumValue = min,
        MaximumValue = max,
        Unit = "per sqm",
        EffectiveDate = new DateOnly(2026, 1, 1),
    };

    // --- Land ---

    [Fact]
    public void CalculateLand_NormalCase_IsAreaTimesRate()
    {
        var land = new Land { Area = 500m };
        var schedule = Schedule(rate: 1000m);

        var result = ValuationCalculator.CalculateLand(land, schedule);

        result.Method.ShouldBe(ValuationMethod.SmvBased);
        result.MarketValue.ShouldBe(500_000m);
        result.Breakdown["Area"].ShouldBe(500m);
        result.Breakdown["Rate"].ShouldBe(1000m);
    }

    [Fact]
    public void CalculateLand_ZeroArea_IsZeroNotAnError()
    {
        var land = new Land { Area = 0m };
        var schedule = Schedule(rate: 1000m);

        var result = ValuationCalculator.CalculateLand(land, schedule);

        result.MarketValue.ShouldBe(0m);
    }

    [Fact]
    public void CalculateLand_LocationFactor_AdjustsValue()
    {
        var land = new Land { Area = 100m, LocationFactor = 1.2m };
        var schedule = Schedule(rate: 1000m);

        var result = ValuationCalculator.CalculateLand(land, schedule);

        result.MarketValue.ShouldBe(120_000m);
    }

    [Fact]
    public void CalculateLand_NoLocationFactor_DefaultsToOne()
    {
        var land = new Land { Area = 100m, LocationFactor = null };
        var schedule = Schedule(rate: 1000m);

        var result = ValuationCalculator.CalculateLand(land, schedule);

        result.MarketValue.ShouldBe(100_000m);
    }

    [Fact]
    public void CalculateLand_AboveScheduleMaximum_ClampsToMaximum()
    {
        var land = new Land { Area = 1000m };
        var schedule = Schedule(rate: 1000m, max: 500_000m);

        var result = ValuationCalculator.CalculateLand(land, schedule);

        result.MarketValue.ShouldBe(500_000m);
        result.Breakdown["ValueBeforeClamp"].ShouldBe(1_000_000m);
    }

    [Fact]
    public void CalculateLand_BelowScheduleMinimum_ClampsToMinimum()
    {
        var land = new Land { Area = 1m };
        var schedule = Schedule(rate: 100m, min: 50_000m);

        var result = ValuationCalculator.CalculateLand(land, schedule);

        result.MarketValue.ShouldBe(50_000m);
    }

    // --- Building ---

    [Fact]
    public void CalculateBuilding_NormalCase_IsFloorAreaTimesRateTimesCompletion()
    {
        var building = new Building { TotalFloorArea = 200m, CompletionPercentage = 100m };
        var schedule = Schedule(rate: 15_000m);

        var result = ValuationCalculator.CalculateBuilding(building, schedule);

        result.Method.ShouldBe(ValuationMethod.SmvBased);
        result.MarketValue.ShouldBe(3_000_000m);
    }

    [Fact]
    public void CalculateBuilding_PartiallyComplete_ReducesValueProportionally()
    {
        var building = new Building { TotalFloorArea = 200m, CompletionPercentage = 50m };
        var schedule = Schedule(rate: 15_000m);

        var result = ValuationCalculator.CalculateBuilding(building, schedule);

        result.MarketValue.ShouldBe(1_500_000m);
    }

    [Fact]
    public void CalculateBuilding_ZeroCompletion_IsZero()
    {
        var building = new Building { TotalFloorArea = 200m, CompletionPercentage = 0m };
        var schedule = Schedule(rate: 15_000m);

        var result = ValuationCalculator.CalculateBuilding(building, schedule);

        result.MarketValue.ShouldBe(0m);
    }

    [Fact]
    public void CalculateBuilding_MissingYearConstructed_DoesNotThrow()
    {
        // No age-based depreciation is computed at all (DOMAIN VERIFICATION
        // REQUIRED — see ValuationCalculator's doc comment), so a building
        // with no YearConstructed on file must still value cleanly.
        var building = new Building { TotalFloorArea = 100m, CompletionPercentage = 100m, YearConstructed = null };
        var schedule = Schedule(rate: 10_000m);

        var result = ValuationCalculator.CalculateBuilding(building, schedule);

        result.MarketValue.ShouldBe(1_000_000m);
    }

    // --- Machinery (LGC §224(a) / §225) ---

    // The §225 floor comes from configuration; 20 here mirrors the cited
    // statutory text and is passed in, never read from a code constant.
    private static readonly MachineryValuationParameters Section225 = new(20m);

    [Fact]
    public void CalculateMachinery_BrandNew_IsAcquisitionCostIncludingCharges()
    {
        var machinery = new Machinery
        {
            IsBrandNew = true,
            AcquisitionCost = 100_000m,
            InstallationCost = 20_000m,
            OtherCost = 5_000m,
        };

        var result = ValuationCalculator.CalculateMachinery(machinery, Section225);

        result.Method.ShouldBe(ValuationMethod.AcquisitionCost);
        result.MarketValue.ShouldBe(125_000m);
    }

    [Fact]
    public void CalculateMachinery_BrandNew_IgnoresLifeSpans()
    {
        var machinery = new Machinery { IsBrandNew = true, AcquisitionCost = 100_000m, EconomicLifeYears = 10, RemainingLifeYears = 2 };

        ValuationCalculator.CalculateMachinery(machinery, Section225).MarketValue.ShouldBe(100_000m);
    }

    [Fact]
    public void CalculateMachinery_Used_UsesReplacementCostNotAcquisitionCost()
    {
        var machinery = new Machinery
        {
            AcquisitionCost = 400_000m,
            ReplacementCost = 1_000_000m,
            EconomicLifeYears = 10,
            RemainingLifeYears = 5,
        };

        var result = ValuationCalculator.CalculateMachinery(machinery, Section225);

        result.Method.ShouldBe(ValuationMethod.ReplacementCost);
        result.MarketValue.ShouldBe(500_000m);
        result.Breakdown["MinimumApplied"].ShouldBe(0m);
    }

    [Fact]
    public void CalculateMachinery_Used_NeverBelowSection225Floor()
    {
        var machinery = new Machinery { ReplacementCost = 1_000_000m, EconomicLifeYears = 10, RemainingLifeYears = 1 };

        var result = ValuationCalculator.CalculateMachinery(machinery, Section225);

        result.Breakdown["DepreciatedValue"].ShouldBe(100_000m);
        result.MarketValue.ShouldBe(200_000m);
        result.Breakdown["MinimumApplied"].ShouldBe(1m);
        result.Breakdown["MinimumRemainingValuePercent"].ShouldBe(20m);
    }

    [Fact]
    public void CalculateMachinery_Used_ExactlyAtFloor_IsNotMarkedAsFloored()
    {
        var machinery = new Machinery { ReplacementCost = 1_000_000m, EconomicLifeYears = 10, RemainingLifeYears = 2 };

        var result = ValuationCalculator.CalculateMachinery(machinery, Section225);

        result.MarketValue.ShouldBe(200_000m);
        result.Breakdown["MinimumApplied"].ShouldBe(0m);
    }

    [Fact]
    public void CalculateMachinery_Used_PastEconomicLife_HoldsAtFloorNotZero()
    {
        var machinery = new Machinery
        {
            ReplacementCost = 100_000m,
            EconomicLifeYears = 10,
            RemainingLifeYears = -3, // clock never reset after full depreciation
        };

        ValuationCalculator.CalculateMachinery(machinery, Section225).MarketValue.ShouldBe(20_000m);
    }

    [Fact]
    public void CalculateMachinery_Used_RemainingLifeAboveEconomicLife_IsClamped()
    {
        var machinery = new Machinery { ReplacementCost = 100_000m, EconomicLifeYears = 10, RemainingLifeYears = 15 };

        ValuationCalculator.CalculateMachinery(machinery, Section225).MarketValue.ShouldBe(100_000m);
    }

    [Fact]
    public void CalculateMachinery_FloorPercentIsTakenFromParameters()
    {
        var machinery = new Machinery { ReplacementCost = 100_000m, EconomicLifeYears = 10, RemainingLifeYears = 0 };

        ValuationCalculator.CalculateMachinery(machinery, new MachineryValuationParameters(0m)).MarketValue.ShouldBe(0m);
    }

    [Fact]
    public void CalculateMachinery_Used_KeepsDecimalPrecision()
    {
        var machinery = new Machinery { ReplacementCost = 1_000_000m, EconomicLifeYears = 3, RemainingLifeYears = 2 };

        var result = ValuationCalculator.CalculateMachinery(machinery, Section225);

        // Unrounded here; rounding to centavos happens where values are stored.
        decimal.Round(result.MarketValue, 2).ShouldBe(666_666.67m);
    }

    [Theory]
    [InlineData(null, 10, 5, "replacement or reproduction cost")]
    [InlineData(100_000, null, 5, "estimated economic life")]
    [InlineData(100_000, 0, 0, "estimated economic life")]
    [InlineData(100_000, 10, null, "remaining economic life")]
    public void MissingMachineryInputs_Used_ReportsEachGap(int? replacementCost, int? economicLife, int? remainingLife, string expected)
    {
        // Never substitute acquisition cost or assume a life span (CLAUDE.md §5, §7).
        var machinery = new Machinery
        {
            AcquisitionCost = 100_000m,
            ReplacementCost = replacementCost,
            EconomicLifeYears = economicLife,
            RemainingLifeYears = remainingLife,
        };

        ValuationCalculator.MissingMachineryInputs(machinery)!.ShouldContain(expected);
        Should.Throw<InvalidOperationException>(() => ValuationCalculator.CalculateMachinery(machinery, Section225));
    }

    [Fact]
    public void MissingMachineryInputs_BrandNew_NeedsNothingMore() =>
        ValuationCalculator.MissingMachineryInputs(new Machinery { IsBrandNew = true, AcquisitionCost = 1m }).ShouldBeNull();
}
