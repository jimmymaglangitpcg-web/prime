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
/// a resolved SMV rate, construction cost or depreciation table. Where a
/// legally-mandated method has no configured table, no formula is invented:
/// a building priced at the older rate by classification and use is not
/// depreciated (<see cref="CalculateBuildingPortion"/>); one priced on the
/// SMV's construction cost is depreciated by the SMV's own table
/// (<see cref="CalculateBuildingByCost"/>).
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
        "AppraisedValue", "Share",
        "IsBrandNew", "AcquisitionCost", "InstallationCost", "OtherCost", "CostItems", "TotalAcquisitionCost",
        "CostInsuranceFreight", "ExchangeRateAtAcquisition", "ExchangeRateAtValuation", "PriceIndexAtAcquisition", "PriceIndexAtValuation",
        "PriceIndexFactor", "OtherExpenses",
        "ReplacementCost", "YearsInUse", "EconomicLifeYears", "RemainingLifeYears", "RemainingFraction", "MaximumYearlyDepreciationPercent",
        "BaseValue", "AdditionalItemsCost", "TotalConstructionCost", "AdjustmentPercent", "ValueAdjustment",
        "Age", "DepreciationPercent", "DepreciationCapped", "DepreciationCarriedOver", "Depreciation",
        "DepreciatedValue", "InOperation", "MinimumRemainingValuePercent", "MinimumRemainingValue", "MinimumApplied",
        "ValueBeforeClamp", "MinimumValue", "MaximumValue",
        "MarketValueBeforeRounding", "RoundingStep", "MarketValue",
    ];

    /// <summary>
    /// The row's market value rounded to the configured step (e.g. the nearest ten), half away
    /// from zero, recording the value before rounding and the step; no step: unchanged
    /// (docs/analysis/valuation-foundation.md §4.4, [C5]).
    /// </summary>
    public static ValuationCalculationResult WithRounding(ValuationCalculationResult result, decimal? step)
    {
        if (step is not { } s || s <= 0)
        {
            return result;
        }
        var rounded = Math.Round(result.MarketValue / s, 0, MidpointRounding.AwayFromZero) * s;
        var breakdown = new Dictionary<string, decimal>(result.Breakdown)
        {
            ["MarketValueBeforeRounding"] = result.MarketValue, ["RoundingStep"] = s, ["MarketValue"] = rounded,
        };
        return result with { MarketValue = rounded, Breakdown = breakdown };
    }

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
    /// schedule's limits. Depreciation is not applied. Used only where the
    /// SMV has no construction costs (legacy and DEMO SMVs; valuation-foundation.md §4.5).
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

    /// <summary>Breakdown key prefix for one named input of an independent appraisal, e.g. "Input:Gross income".</summary>
    public const string InputKeyPrefix = "Input:";

    /// <summary>
    /// A row valued by an independent appraisal (valuation-foundation.md §4.7): its share of the appraised
    /// value (1 for the whole; a floor-area or land-area share when the value covers several rows), with the
    /// appraisal's named inputs kept for the record. Nothing is computed from the inputs (Q13).
    /// </summary>
    public static ValuationCalculationResult FromIndependentAppraisal(decimal appraisedValue, decimal share, decimal value,
        IReadOnlyList<(string Name, decimal Value)> inputs)
    {
        var breakdown = new Dictionary<string, decimal> { ["AppraisedValue"] = appraisedValue, ["MarketValue"] = value };
        if (share != 1m)
        {
            breakdown["Share"] = share;
        }
        foreach (var (name, v) in inputs)
        {
            breakdown[InputKeyPrefix + name] = v;
        }
        return new ValuationCalculationResult(ValuationMethod.IndependentAppraisal, value, breakdown);
    }

    /// <summary>Breakdown key prefix for one extra item's cost, e.g. "ExtraItem:FENCE".</summary>
    public const string ExtraItemKeyPrefix = "ExtraItem:";

    /// <summary>
    /// One use portion of a building valued on the SMV's construction cost (LAM Bk III p.72;
    /// docs/analysis/valuation-foundation.md §4.5), as the FAAS lays it out:
    /// core = floor area × BUCC; + the extra items (each priced from the SMV) = total construction
    /// cost; × completion; depreciation = that × the depreciation percent; market value = the rest.
    /// The depreciation percent is the table's for the building's age, or the one carried over
    /// from the last posted valuation (Q10); the caller decides which and says so.
    /// </summary>
    public static ValuationCalculationResult CalculateBuildingByCost(decimal floorArea, decimal costPerSquareMetre,
        IReadOnlyList<(string Code, decimal Cost)> extraItems, decimal completionPercentage, BuildingDepreciationInput depreciation)
    {
        var core = floorArea * costPerSquareMetre;
        var extras = extraItems.Sum(x => x.Cost);
        var total = (core + extras) * completionPercentage / 100m;
        var amount = total * depreciation.Percent / 100m;
        var marketValue = total - amount;
        var breakdown = new Dictionary<string, decimal>
        {
            ["FloorArea"] = floorArea, ["Rate"] = costPerSquareMetre, ["BaseValue"] = core, ["AdditionalItemsCost"] = extras,
            ["CompletionPercentage"] = completionPercentage, ["TotalConstructionCost"] = total,
            ["DepreciationPercent"] = depreciation.Percent, ["DepreciationCarriedOver"] = depreciation.CarriedOver ? 1m : 0m,
            ["Depreciation"] = amount, ["MarketValue"] = marketValue,
        };
        foreach (var (code, cost) in extraItems.GroupBy(x => x.Code).Select(g => (g.Key, g.Sum(x => x.Cost))))
        {
            breakdown[ExtraItemKeyPrefix + code] = cost;
        }
        if (depreciation.Age is { } age)
        {
            breakdown["Age"] = age;
        }
        if (depreciation.Capped)
        {
            breakdown["DepreciationCapped"] = 1m;
        }
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
    /// This is the entered-replacement-cost method, kept for legacy records and machinery without
    /// index data (valuation-foundation.md §4.6); the derived method is
    /// <see cref="CalculateMachineryDerived"/>. Machinery not in operation gets no minimum (Q12).
    /// </summary>
    public static ValuationCalculationResult CalculateMachinery(Machinery machinery, MachineryValuationParameters parameters)
    {
        if (MissingMachineryInputs(machinery) is { } missing)
        {
            throw new InvalidOperationException($"Machinery cannot be valued under LGC §224 without: {missing}.");
        }

        if (machinery.IsBrandNew)
        {
            var items = machinery.CostItems.Sum(i => i.Amount);
            var acquisitionCost = machinery.AcquisitionCost + (machinery.InstallationCost ?? 0m) + (machinery.OtherCost ?? 0m) + items;
            var brandNew = new Dictionary<string, decimal>
            {
                ["IsBrandNew"] = 1m,
                ["AcquisitionCost"] = machinery.AcquisitionCost,
                ["InstallationCost"] = machinery.InstallationCost ?? 0m,
                ["OtherCost"] = machinery.OtherCost ?? 0m,
                ["TotalAcquisitionCost"] = acquisitionCost,
                ["MarketValue"] = acquisitionCost,
            };
            if (items != 0m)
            {
                brandNew["CostItems"] = items;
            }
            return new ValuationCalculationResult(ValuationMethod.AcquisitionCost, acquisitionCost, brandNew);
        }

        var replacementCost = machinery.ReplacementCost!.Value;
        var economicLife = machinery.EconomicLifeYears!.Value;
        var remainingLife = Math.Clamp(machinery.RemainingLifeYears!.Value, 0, economicLife);
        var remainingFraction = (decimal)remainingLife / economicLife;
        var depreciatedValue = replacementCost * remainingFraction;
        var minimumRemainingValue = replacementCost * parameters.MinimumRemainingValuePercent / 100m;
        // LGC §225: the minimum holds "for so long as the machinery is useful and in operation" (Q12).
        var floorApplied = machinery.IsInOperation && depreciatedValue < minimumRemainingValue;
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
            ["InOperation"] = machinery.IsInOperation ? 1m : 0m,
            ["MinimumApplied"] = floorApplied ? 1m : 0m,
            ["MarketValue"] = marketValue,
        });
    }

    /// <summary>Completed years from <paramref name="start"/> to <paramref name="date"/>, never below zero.</summary>
    public static int YearsBetween(DateOnly start, DateOnly date)
    {
        var years = date.Year - start.Year;
        if (date < start.AddYears(years))
        {
            years--;
        }
        return Math.Max(0, years);
    }

    /// <summary>
    /// Machinery that is not brand-new, from its acquisition cost (LAM Bk III pp.73–75, Formulas 7–12;
    /// docs/analysis/valuation-foundation.md §4.6, Q11, Q12):
    /// <list type="bullet">
    /// <item>replacement cost = cost, insurance and freight × (exchange rate at valuation ÷ at acquisition,
    /// imported machinery only) × (price index of the valuation year ÷ of the acquisition year), plus the
    /// other acquisition expenses at their recorded cost;</item>
    /// <item>depreciation = replacement cost × the smaller of years of use ÷ economic life and the configured
    /// yearly maximum × years of use (LGC §225: not exceeding 5% a year), at most all of it;</item>
    /// <item>market value = the rest, but not below the configured minimum remaining value while the machine
    /// is in operation.</item>
    /// </list>
    /// Whether the other expenses are trended too is DOMAIN VERIFICATION REQUIRED ([C4]); they are not.
    /// </summary>
    public static ValuationCalculationResult CalculateMachineryDerived(MachineryDerivationInput input, MachineryValuationParameters parameters)
    {
        if (parameters.MaximumYearlyDepreciationPercent is not { } maxRate)
        {
            throw new InvalidOperationException("The maximum yearly depreciation of machinery is not configured.");
        }
        var exchangeFactor = input.ExchangeRateAtAcquisition is { } fxA && input.ExchangeRateAtValuation is { } fxV ? fxV / fxA : 1m;
        var indexFactor = input.PriceIndexAtValuation / input.PriceIndexAtAcquisition;
        var replacementCost = input.CostInsuranceFreight * exchangeFactor * indexFactor + input.OtherExpenses;
        var byLife = (decimal)input.YearsInUse / input.EconomicLifeYears;
        var byCap = maxRate * input.YearsInUse / 100m;
        var fraction = Math.Min(1m, Math.Min(byLife, byCap));
        var depreciation = replacementCost * fraction;
        var depreciatedValue = replacementCost - depreciation;
        var minimum = replacementCost * parameters.MinimumRemainingValuePercent / 100m;
        var floorApplied = input.InOperation && depreciatedValue < minimum;
        var marketValue = floorApplied ? minimum : depreciatedValue;
        var breakdown = new Dictionary<string, decimal>
        {
            ["IsBrandNew"] = 0m,
            ["CostInsuranceFreight"] = input.CostInsuranceFreight,
            ["PriceIndexAtAcquisition"] = input.PriceIndexAtAcquisition,
            ["PriceIndexAtValuation"] = input.PriceIndexAtValuation,
            ["PriceIndexFactor"] = indexFactor,
            ["OtherExpenses"] = input.OtherExpenses,
            ["ReplacementCost"] = replacementCost,
            ["YearsInUse"] = input.YearsInUse,
            ["EconomicLifeYears"] = input.EconomicLifeYears,
            ["MaximumYearlyDepreciationPercent"] = maxRate,
            ["DepreciationPercent"] = fraction * 100m,
            ["DepreciationCapped"] = byCap < byLife ? 1m : 0m,
            ["Depreciation"] = depreciation,
            ["DepreciatedValue"] = depreciatedValue,
            ["InOperation"] = input.InOperation ? 1m : 0m,
            ["MinimumRemainingValuePercent"] = parameters.MinimumRemainingValuePercent,
            ["MinimumRemainingValue"] = minimum,
            ["MinimumApplied"] = floorApplied ? 1m : 0m,
            ["MarketValue"] = marketValue,
        };
        if (input.ExchangeRateAtAcquisition is { } a && input.ExchangeRateAtValuation is { } v)
        {
            breakdown["ExchangeRateAtAcquisition"] = a;
            breakdown["ExchangeRateAtValuation"] = v;
        }
        return new ValuationCalculationResult(ValuationMethod.DerivedReplacementCost, marketValue, breakdown);
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

/// <summary>
/// The depreciation a building is valued with: the percent, the age it was read for (null when
/// carried over), whether it was carried over from the last posted valuation, and whether the
/// table's minimum remaining value capped it.
/// </summary>
public sealed record BuildingDepreciationInput(decimal Percent, int? Age, bool CarriedOver, bool Capped);

/// <summary>
/// What a derived machinery valuation reads: the cost, insurance and freight and the other expenses
/// (pesos, at acquisition), the exchange rates (imported only), the price indices of the acquisition
/// and valuation years, the years of use, the economic life and whether the machine is in operation.
/// </summary>
public sealed record MachineryDerivationInput(decimal CostInsuranceFreight, decimal OtherExpenses, decimal? ExchangeRateAtAcquisition,
    decimal? ExchangeRateAtValuation, decimal PriceIndexAtAcquisition, decimal PriceIndexAtValuation, int YearsInUse, int EconomicLifeYears,
    bool InOperation);

/// <summary>One adjustment factor as applied: its code, name and the percent in force.</summary>
public sealed record LandAdjustmentInput(string Code, string Name, decimal Percent);
