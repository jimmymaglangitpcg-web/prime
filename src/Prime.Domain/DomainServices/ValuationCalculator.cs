using Prime.Domain.Entities;
using Prime.Domain.Enums;

namespace Prime.Domain.DomainServices;

/// <summary>
/// CLAUDE.md §30 valuation engine — pure, EF-free calculation logic. Takes
/// already-loaded entities and returns a reproducible breakdown; it does not
/// query a database or resolve "which SMV schedule applies" (that's
/// <c>Prime.Application.Features.Valuation.ValuationService</c>'s job), so
/// it can be unit-tested directly (see docs/DEVELOPMENT-ROADMAP.md Phase 5).
///
/// CLAUDE.md forbids inventing rates or assumptions (§5-§7): every input
/// used below is either a value already entered on the entity (e.g.
/// <see cref="Machinery.RemainingLifeYears"/>, an appraiser's own figure) or
/// a resolved <see cref="SmvSchedule"/> rate. Where a legally-mandated
/// method exists but no sourced rule is available yet — building
/// depreciation by age, which would need an economic-life-by-building-type
/// table nobody has supplied — no formula is invented; the corresponding
/// output is simply not computed. See the Building method below.
/// </summary>
public static class ValuationCalculator
{
    /// <summary>
    /// MarketValue = Area × SMV rate × LocationFactor, clamped to the
    /// schedule's Minimum/MaximumValue if set. <see cref="Land.LocationFactor"/>
    /// already exists on the entity for exactly this purpose (§24) — it is
    /// not a new adjustment invented here.
    /// </summary>
    public static ValuationCalculationResult CalculateLand(Land land, SmvSchedule schedule)
    {
        var rate = schedule.MarketValue;
        var locationFactor = land.LocationFactor ?? 1m;
        var baseValue = land.Area * rate;
        var valueBeforeClamp = baseValue * locationFactor;
        var marketValue = ClampToRange(valueBeforeClamp, schedule.MinimumValue, schedule.MaximumValue);

        var breakdown = new Dictionary<string, decimal>
        {
            ["Area"] = land.Area,
            ["Rate"] = rate,
            ["LocationFactor"] = locationFactor,
            ["BaseValue"] = baseValue,
            ["ValueBeforeClamp"] = valueBeforeClamp,
            ["MarketValue"] = marketValue,
        };
        AddClampBoundsIfPresent(breakdown, schedule.MinimumValue, schedule.MaximumValue);

        return new ValuationCalculationResult(ValuationMethod.SmvBased, marketValue, breakdown);
    }

    /// <summary>
    /// MarketValue = TotalFloorArea × SMV rate × (CompletionPercentage / 100),
    /// clamped to the schedule's Minimum/MaximumValue if set.
    /// <see cref="Building.CompletionPercentage"/> is an assessor-entered
    /// field already on the entity, not invented here.
    ///
    /// Deliberately does NOT depreciate by building age: that would require
    /// an assumed economic-life-in-years per building type, and no such
    /// figure is entered anywhere on <see cref="Building"/> or sourced from
    /// an ordinance — inventing one would violate CLAUDE.md's "never invent
    /// a rate" rule. <c>DOMAIN VERIFICATION REQUIRED</c> before an
    /// age-based depreciation step can be added; <see cref="Building.Depreciation"/>/
    /// <see cref="Building.DepreciatedValue"/> are left unset by this method.
    /// </summary>
    public static ValuationCalculationResult CalculateBuilding(Building building, SmvSchedule schedule)
    {
        var rate = schedule.MarketValue;
        var completionFactor = building.CompletionPercentage / 100m;
        var baseValue = building.TotalFloorArea * rate;
        var valueBeforeClamp = baseValue * completionFactor;
        var marketValue = ClampToRange(valueBeforeClamp, schedule.MinimumValue, schedule.MaximumValue);

        var breakdown = new Dictionary<string, decimal>
        {
            ["TotalFloorArea"] = building.TotalFloorArea,
            ["Rate"] = rate,
            ["CompletionPercentage"] = building.CompletionPercentage,
            ["BaseValue"] = baseValue,
            ["ValueBeforeClamp"] = valueBeforeClamp,
            ["MarketValue"] = marketValue,
        };
        AddClampBoundsIfPresent(breakdown, schedule.MinimumValue, schedule.MaximumValue);

        return new ValuationCalculationResult(ValuationMethod.SmvBased, marketValue, breakdown);
    }

    /// <summary>
    /// Replacement-cost method: MarketValue = (AcquisitionCost +
    /// InstallationCost + OtherCost) × (RemainingLifeYears / EconomicLifeYears),
    /// straight-line, using only figures already entered on the entity
    /// (§26 — an appraiser's own acquisition/life-span data, not a code-level
    /// assumption). When either life-span field is missing, no depreciation
    /// is applied — the full cost stands, rather than guessing a life span.
    /// Fully-depreciated machinery (RemainingLifeYears = 0) floors at 0, not negative.
    /// </summary>
    public static ValuationCalculationResult CalculateMachinery(Machinery machinery)
    {
        var totalCost = machinery.AcquisitionCost + (machinery.InstallationCost ?? 0m) + (machinery.OtherCost ?? 0m);

        decimal remainingFraction = 1m;
        var hasLifeSpanData = machinery.EconomicLifeYears is > 0 && machinery.RemainingLifeYears is not null;
        if (hasLifeSpanData)
        {
            var economicLife = machinery.EconomicLifeYears!.Value;
            var remainingLife = Math.Clamp(machinery.RemainingLifeYears!.Value, 0, economicLife);
            remainingFraction = (decimal)remainingLife / economicLife;
        }

        var marketValue = totalCost * remainingFraction;

        var breakdown = new Dictionary<string, decimal>
        {
            ["AcquisitionCost"] = machinery.AcquisitionCost,
            ["InstallationCost"] = machinery.InstallationCost ?? 0m,
            ["OtherCost"] = machinery.OtherCost ?? 0m,
            ["TotalCost"] = totalCost,
            ["RemainingFraction"] = remainingFraction,
            ["MarketValue"] = marketValue,
        };
        if (hasLifeSpanData)
        {
            breakdown["EconomicLifeYears"] = machinery.EconomicLifeYears!.Value;
            breakdown["RemainingLifeYears"] = machinery.RemainingLifeYears!.Value;
        }

        return new ValuationCalculationResult(ValuationMethod.ReplacementCost, marketValue, breakdown);
    }

    private static decimal ClampToRange(decimal value, decimal? min, decimal? max)
    {
        if (min is not null && value < min.Value)
        {
            value = min.Value;
        }
        if (max is not null && value > max.Value)
        {
            value = max.Value;
        }
        return value;
    }

    private static void AddClampBoundsIfPresent(Dictionary<string, decimal> breakdown, decimal? min, decimal? max)
    {
        if (min is not null)
        {
            breakdown["MinimumValue"] = min.Value;
        }
        if (max is not null)
        {
            breakdown["MaximumValue"] = max.Value;
        }
    }
}
