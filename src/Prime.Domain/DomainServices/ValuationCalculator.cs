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
    /// The order a breakdown is read in: inputs, then intermediate values,
    /// then the result. Stored breakdowns (jsonb) do not keep key order, so
    /// displays sort by this list; a key not listed here goes just before
    /// MarketValue. Keep it in step with the keys the methods below write.
    /// </summary>
    public static readonly IReadOnlyList<string> BreakdownOrder =
    [
        "Area", "TotalFloorArea", "FloorArea", "Quantity", "Rate", "LocationFactor", "CompletionPercentage",
        "IsBrandNew", "AcquisitionCost", "InstallationCost", "OtherCost", "TotalAcquisitionCost",
        "ReplacementCost", "EconomicLifeYears", "RemainingLifeYears", "RemainingFraction", "DepreciatedValue",
        "MinimumRemainingValuePercent", "MinimumRemainingValue", "MinimumApplied",
        "BaseValue", "AdditionalItemsCost", "TotalConstructionCost", "AdjustmentPercent", "ValueAdjustment", "ValueBeforeClamp", "MinimumValue", "MaximumValue", "MarketValue",
    ];

    /// <summary>Breakdown key prefix for one adjustment factor's percent, e.g. "Adjustment:CORNER".</summary>
    public const string AdjustmentKeyPrefix = "Adjustment:";

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
    /// One land strip (MRPAAO Att. 1): base value = area × SMV rate; value
    /// adjustment = base value × Σ adjustment percents / 100 (the factors add —
    /// DOMAIN VERIFICATION REQUIRED whether they compound); then the legacy
    /// <c>LocationFactor</c> multiplier when the land still carries one; then
    /// the schedule's minimum/maximum. Each factor's percent is kept in the
    /// breakdown under <see cref="AdjustmentKeyPrefix"/> + its code.
    /// </summary>
    public static ValuationCalculationResult CalculateLandStrip(decimal area, SmvSchedule schedule, decimal? locationFactor,
        IReadOnlyList<LandAdjustmentInput> adjustments)
    {
        var rate = schedule.MarketValue;
        var baseValue = area * rate;
        var breakdown = new Dictionary<string, decimal> { ["Area"] = area, ["Rate"] = rate, ["BaseValue"] = baseValue };
        var value = baseValue;
        if (adjustments.Count > 0)
        {
            var percent = adjustments.Sum(a => a.Percent);
            var valueAdjustment = baseValue * percent / 100m;
            foreach (var a in adjustments)
            {
                breakdown[AdjustmentKeyPrefix + a.Code] = a.Percent;
            }
            breakdown["AdjustmentPercent"] = percent;
            breakdown["ValueAdjustment"] = valueAdjustment;
            value += valueAdjustment;
        }
        if (locationFactor is { } factor)
        {
            breakdown["LocationFactor"] = factor;
            value *= factor;
        }
        var marketValue = ClampToRange(value, schedule.MinimumValue, schedule.MaximumValue);
        breakdown["ValueBeforeClamp"] = value;
        breakdown["MarketValue"] = marketValue;
        AddClampBoundsIfPresent(breakdown, schedule.MinimumValue, schedule.MaximumValue);
        return new ValuationCalculationResult(ValuationMethod.SmvBased, marketValue, breakdown);
    }

    /// <summary>
    /// Trees, plants and other land improvements (MRPAAO Att. 1): market value
    /// = number × the SMV rate for their kind, within the schedule's limits.
    /// </summary>
    public static ValuationCalculationResult CalculateImprovement(decimal quantity, SmvSchedule schedule)
    {
        var rate = schedule.MarketValue;
        var baseValue = quantity * rate;
        var marketValue = ClampToRange(baseValue, schedule.MinimumValue, schedule.MaximumValue);
        var breakdown = new Dictionary<string, decimal>
        {
            ["Quantity"] = quantity, ["Rate"] = rate, ["BaseValue"] = baseValue, ["ValueBeforeClamp"] = baseValue, ["MarketValue"] = marketValue,
        };
        AddClampBoundsIfPresent(breakdown, schedule.MinimumValue, schedule.MaximumValue);
        return new ValuationCalculationResult(ValuationMethod.SmvBased, marketValue, breakdown);
    }

    /// <summary>
    /// One use portion of a building (MRPAAO Att. 2 "Property Appraisal"):
    /// building core = floor area × the SMV rate; + the cost of its
    /// additional items = total construction cost; × completion; then the
    /// schedule's limits. Depreciation is not applied (plan A9: no
    /// configured table yet — DOMAIN VERIFICATION REQUIRED).
    /// </summary>
    public static ValuationCalculationResult CalculateBuildingPortion(decimal floorArea, SmvSchedule schedule, decimal additionalItemsCost,
        decimal completionPercentage)
    {
        var rate = schedule.MarketValue;
        var core = floorArea * rate;
        var constructionCost = core + additionalItemsCost;
        var value = constructionCost * completionPercentage / 100m;
        var marketValue = ClampToRange(value, schedule.MinimumValue, schedule.MaximumValue);
        var breakdown = new Dictionary<string, decimal>
        {
            ["FloorArea"] = floorArea, ["Rate"] = rate, ["BaseValue"] = core, ["AdditionalItemsCost"] = additionalItemsCost,
            ["TotalConstructionCost"] = constructionCost, ["CompletionPercentage"] = completionPercentage,
            ["ValueBeforeClamp"] = value, ["MarketValue"] = marketValue,
        };
        AddClampBoundsIfPresent(breakdown, schedule.MinimumValue, schedule.MaximumValue);
        return new ValuationCalculationResult(ValuationMethod.SmvBased, marketValue, breakdown);
    }

    /// <summary>
    /// Spreads <paramref name="cost"/> over portions in proportion to their
    /// floor area, to the centavo; the last portion takes the remainder so
    /// the shares add up exactly.
    /// </summary>
    public static IReadOnlyList<decimal> SpreadByArea(decimal cost, IReadOnlyList<decimal> areas)
    {
        var total = areas.Sum();
        var shares = new List<decimal>(areas.Count);
        for (var i = 0; i < areas.Count - 1; i++)
        {
            shares.Add(total == 0 ? 0 : Math.Round(cost * areas[i] / total, 2, MidpointRounding.AwayFromZero));
        }
        if (areas.Count > 0)
        {
            shares.Add(cost - shares.Sum());
        }
        return shares;
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
    /// Why <paramref name="machinery"/> cannot be valued under LGC §224, or
    /// null when it can. Brand-new machinery needs nothing more; any other
    /// machinery needs its replacement/reproduction cost and both life spans,
    /// because §224(a) prescribes that formula "in all other cases" — the
    /// calculator never substitutes acquisition cost or assumes a life span.
    /// </summary>
    public static string? MissingMachineryInputs(Machinery machinery)
    {
        if (machinery.IsBrandNew)
        {
            return null;
        }
        var missing = new List<string>();
        if (machinery.ReplacementCost is null)
        {
            missing.Add("replacement or reproduction cost");
        }
        if (machinery.EconomicLifeYears is not > 0)
        {
            missing.Add("estimated economic life (years, > 0)");
        }
        if (machinery.RemainingLifeYears is null)
        {
            missing.Add("remaining economic life (years)");
        }
        return missing.Count == 0 ? null : string.Join(", ", missing);
    }

    /// <summary>
    /// LGC §224(a) and §225, using only figures entered by the appraiser plus
    /// the configured §225 floor (<paramref name="parameters"/>):
    /// <list type="bullet">
    /// <item>Brand-new: MarketValue = acquisition cost (AcquisitionCost +
    /// InstallationCost + OtherCost; §224(b) counts freight, duties,
    /// installation and similar charges as part of acquisition cost).</item>
    /// <item>All other machinery: MarketValue = ReplacementCost ×
    /// (RemainingLifeYears / EconomicLifeYears), but never below
    /// MinimumRemainingValuePercent of ReplacementCost (§225 proviso). The
    /// remaining life is clamped to 0…EconomicLifeYears.</item>
    /// </list>
    /// Throws when <see cref="MissingMachineryInputs"/> is not null — callers
    /// check first and report the gap to the user.
    /// DOMAIN VERIFICATION REQUIRED: whether §225's "not exceeding 5% … for
    /// each year of use" limits the §224 life ratio, and how machinery that
    /// is no longer "useful and in operation" is treated; neither is applied.
    /// </summary>
    public static ValuationCalculationResult CalculateMachinery(Machinery machinery, MachineryValuationParameters parameters)
    {
        if (MissingMachineryInputs(machinery) is { } missing)
        {
            throw new InvalidOperationException($"Machinery cannot be valued under LGC §224 without: {missing}.");
        }

        if (machinery.IsBrandNew)
        {
            var acquisitionCost = machinery.AcquisitionCost + (machinery.InstallationCost ?? 0m) + (machinery.OtherCost ?? 0m);
            return new ValuationCalculationResult(ValuationMethod.AcquisitionCost, acquisitionCost, new Dictionary<string, decimal>
            {
                ["IsBrandNew"] = 1m,
                ["AcquisitionCost"] = machinery.AcquisitionCost,
                ["InstallationCost"] = machinery.InstallationCost ?? 0m,
                ["OtherCost"] = machinery.OtherCost ?? 0m,
                ["TotalAcquisitionCost"] = acquisitionCost,
                ["MarketValue"] = acquisitionCost,
            });
        }

        var replacementCost = machinery.ReplacementCost!.Value;
        var economicLife = machinery.EconomicLifeYears!.Value;
        var remainingLife = Math.Clamp(machinery.RemainingLifeYears!.Value, 0, economicLife);
        var remainingFraction = (decimal)remainingLife / economicLife;
        var depreciatedValue = replacementCost * remainingFraction;
        var minimumRemainingValue = replacementCost * parameters.MinimumRemainingValuePercent / 100m;
        var floorApplied = depreciatedValue < minimumRemainingValue;
        var marketValue = floorApplied ? minimumRemainingValue : depreciatedValue;

        return new ValuationCalculationResult(ValuationMethod.ReplacementCost, marketValue, new Dictionary<string, decimal>
        {
            ["IsBrandNew"] = 0m,
            ["ReplacementCost"] = replacementCost,
            ["EconomicLifeYears"] = economicLife,
            ["RemainingLifeYears"] = machinery.RemainingLifeYears!.Value,
            ["RemainingFraction"] = remainingFraction,
            ["DepreciatedValue"] = depreciatedValue,
            ["MinimumRemainingValuePercent"] = parameters.MinimumRemainingValuePercent,
            ["MinimumRemainingValue"] = minimumRemainingValue,
            ["MinimumApplied"] = floorApplied ? 1m : 0m,
            ["MarketValue"] = marketValue,
        });
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

/// <summary>One adjustment factor as applied: its code, name and the percent in force.</summary>
public sealed record LandAdjustmentInput(string Code, string Name, decimal Percent);
