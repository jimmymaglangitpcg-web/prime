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

    // --- Machinery ---

    [Fact]
    public void CalculateMachinery_WithinEconomicLife_AppliesStraightLineDepreciation()
    {
        var machinery = new Machinery
        {
            AcquisitionCost = 1_000_000m,
            EconomicLifeYears = 10,
            RemainingLifeYears = 5,
        };

        var result = ValuationCalculator.CalculateMachinery(machinery);

        result.Method.ShouldBe(ValuationMethod.ReplacementCost);
        result.MarketValue.ShouldBe(500_000m);
    }

    [Fact]
    public void CalculateMachinery_IncludesInstallationAndOtherCost()
    {
        var machinery = new Machinery
        {
            AcquisitionCost = 100_000m,
            InstallationCost = 20_000m,
            OtherCost = 5_000m,
            EconomicLifeYears = 10,
            RemainingLifeYears = 10,
        };

        var result = ValuationCalculator.CalculateMachinery(machinery);

        result.MarketValue.ShouldBe(125_000m);
    }

    [Fact]
    public void CalculateMachinery_NoLifeSpanData_ReturnsFullCost()
    {
        // Never invent an assumed economic life (CLAUDE.md §7) — missing
        // life-span data means no depreciation is applied, not a guess.
        var machinery = new Machinery { AcquisitionCost = 100_000m };

        var result = ValuationCalculator.CalculateMachinery(machinery);

        result.MarketValue.ShouldBe(100_000m);
    }

    [Fact]
    public void CalculateMachinery_PastEconomicLife_FloorsAtZeroNotNegative()
    {
        var machinery = new Machinery
        {
            AcquisitionCost = 100_000m,
            EconomicLifeYears = 10,
            RemainingLifeYears = -3, // clock never reset after full depreciation
        };

        var result = ValuationCalculator.CalculateMachinery(machinery);

        result.MarketValue.ShouldBe(0m);
    }

    [Fact]
    public void CalculateMachinery_ZeroEconomicLifeYears_DoesNotDivideByZero()
    {
        var machinery = new Machinery
        {
            AcquisitionCost = 100_000m,
            EconomicLifeYears = 0,
            RemainingLifeYears = 0,
        };

        var result = ValuationCalculator.CalculateMachinery(machinery);

        result.MarketValue.ShouldBe(100_000m);
    }
}
