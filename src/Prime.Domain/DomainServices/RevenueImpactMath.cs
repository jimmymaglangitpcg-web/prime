namespace Prime.Domain.DomainServices;

/// <summary>The revenue compliance of one year (LAM 2025 Book IV pp.116–117, the tax gap approach).</summary>
public sealed record RevenueCompliance(
    decimal TaxableAssessedValue, decimal RatePercent, decimal TaxPotential, decimal ActualCollection, decimal Discounts, decimal TotalCollection,
    decimal TaxGap, decimal? ComplianceRatePercent, decimal? CollectionEfficiencyPercent);

/// <summary>How a scenario's tax compares with the current tax, unit by unit (Book IV p.118 step 4).</summary>
public sealed record TaxImpactSummary(
    int Units, decimal CurrentTax, decimal ScenarioTax, int Lower, int Higher, int Unchanged,
    decimal? SmallestIncrease, decimal? MedianIncrease, decimal? LargestIncrease, decimal? LargestIncreasePercent,
    int Reclassified, decimal ReclassifiedTaxChange);

/// <summary>One unit's tax under the current values and under a scenario.</summary>
public sealed record UnitTax(decimal CurrentTax, decimal ScenarioTax, bool Reclassified);

/// <summary>
/// The arithmetic of the revenue compliance and tax impact study (LAM 2025 Book IV pp.116–118; RA 12001 §17;
/// docs/analysis/smv-preparation-general-revision.md §4.5). Rates and collections are the Treasurer's figures entered on the
/// study; this only computes. Amounts to centavos.
/// </summary>
public static class RevenueImpactMath
{
    public static decimal Tax(decimal assessedValue, decimal ratePercent) => Math.Round(assessedValue * ratePercent / 100m, 2, MidpointRounding.AwayFromZero);

    /// <summary>
    /// Tax potential = taxable assessed value × rate; total collection = actual collection + discounts given; tax gap = potential −
    /// total collection; compliance rate = total collection ÷ potential (the taxpayers' view); collection efficiency = actual
    /// collection ÷ potential (the collector's view). The rates are null when there is no potential.
    /// </summary>
    public static RevenueCompliance Compliance(decimal taxableAssessedValue, decimal ratePercent, decimal actualCollection, decimal discounts)
    {
        var potential = Tax(taxableAssessedValue, ratePercent);
        var total = actualCollection + discounts;
        decimal? Percent(decimal part) => potential > 0 ? Math.Round(part / potential * 100m, 2, MidpointRounding.AwayFromZero) : null;
        return new RevenueCompliance(taxableAssessedValue, ratePercent, potential, actualCollection, discounts, total, potential - total,
            Percent(total), Percent(actualCollection));
    }

    /// <summary>The comparison of a scenario with the current tax over the units (step 4: lower, higher with the range, reclassified).</summary>
    public static TaxImpactSummary Summarize(IReadOnlyList<UnitTax> units)
    {
        var increases = units.Where(u => u.ScenarioTax > u.CurrentTax).Select(u => u.ScenarioTax - u.CurrentTax).Order().ToList();
        decimal? median = increases.Count == 0 ? null
            : increases.Count % 2 == 1 ? increases[increases.Count / 2]
            : Math.Round((increases[increases.Count / 2 - 1] + increases[increases.Count / 2]) / 2, 2, MidpointRounding.AwayFromZero);
        var percentIncreases = units.Where(u => u.ScenarioTax > u.CurrentTax && u.CurrentTax > 0)
            .Select(u => Math.Round((u.ScenarioTax - u.CurrentTax) / u.CurrentTax * 100m, 2, MidpointRounding.AwayFromZero)).ToList();
        var reclassified = units.Where(u => u.Reclassified).ToList();
        return new TaxImpactSummary(
            units.Count, units.Sum(u => u.CurrentTax), units.Sum(u => u.ScenarioTax),
            units.Count(u => u.ScenarioTax < u.CurrentTax), increases.Count, units.Count(u => u.ScenarioTax == u.CurrentTax),
            increases.Count == 0 ? null : increases[0], median, increases.Count == 0 ? null : increases[^1],
            percentIncreases.Count == 0 ? null : percentIncreases.Max(),
            reclassified.Count, reclassified.Sum(u => u.ScenarioTax - u.CurrentTax));
    }
}
