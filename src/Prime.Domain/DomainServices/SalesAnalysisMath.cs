namespace Prime.Domain.DomainServices;

/// <summary>One range of the interval-frequency analysis: its bounds, its midpoint, and the rounded unit values in it.</summary>
public sealed record SalesRange(int Number, decimal Low, decimal Mid, decimal High, IReadOnlyList<decimal> Values)
{
    public int Frequency => Values.Count;
}

/// <summary>
/// The arithmetic of SMV Forms 3–4 and 7–8 (LAM 2025 Annexes IV-C, IV-D, IV-G, IV-H; docs/analysis/
/// smv-preparation-general-revision.md §4.2, Q6–Q7), with the assessor's parameters. The annexes describe the steps; their
/// worked example is for illustration only and is not reproducible by one rule, so PRIME applies the reading below and shows
/// every step. DOMAIN VERIFICATION REQUIRED: the construction of the ranges.
/// <list type="number">
/// <item>Each unit sales value is rounded to the increment (the annexes: the nearest hundred) and sorted lowest to highest.</item>
/// <item>The interval of each value is its rise over the previous one, as a percent; their average is shown.</item>
/// <item>Ranges of ±w% (w defaults to the average interval): a range starts at the lowest value not yet in a range; its
/// midpoint is that value × (1 + w), rounded to the increment; its high is the midpoint × (1 + w); it takes the values up to
/// its high. Each range is counted.</item>
/// <item>The assessor merges ranges with few sales, names the sub-classes from the highest midpoint down, and adopts each
/// unit value; PRIME proposes the frequency-weighted mean of the merged midpoints and never adopts by itself.</item>
/// </list>
/// </summary>
public static class SalesAnalysisMath
{
    /// <summary>The value rounded to the nearest increment (halves up).</summary>
    public static decimal RoundTo(decimal value, decimal increment) =>
        increment <= 0 ? value : Math.Round(value / increment, MidpointRounding.AwayFromZero) * increment;

    /// <summary>The unit price adjusted to the base valuation date by the time factor and the other adjustment (percent, + or −).</summary>
    public static decimal Adjust(decimal unitPrice, decimal timeFactor, decimal otherAdjustmentPercent) =>
        Math.Round(unitPrice * timeFactor * (1m + otherAdjustmentPercent / 100m), 2, MidpointRounding.AwayFromZero);

    /// <summary>The interval of each sorted value over the previous one, in percent to two decimals; null for the first.</summary>
    public static IReadOnlyList<decimal?> Intervals(IReadOnlyList<decimal> sorted) =>
        sorted.Select((v, i) => i == 0 || sorted[i - 1] <= 0 ? (decimal?)null
            : Math.Round((v - sorted[i - 1]) / sorted[i - 1] * 100m, 2, MidpointRounding.AwayFromZero)).ToList();

    /// <summary>The mean of the intervals, to two decimals; null with fewer than two values.</summary>
    public static decimal? AverageInterval(IReadOnlyList<decimal> sorted)
    {
        var intervals = Intervals(sorted).OfType<decimal>().ToList();
        return intervals.Count == 0 ? null : Math.Round(intervals.Average(), 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>The ranges of ±<paramref name="widthPercent"/> over the sorted rounded values (step 3).</summary>
    public static IReadOnlyList<SalesRange> Ranges(IReadOnlyList<decimal> sorted, decimal widthPercent, decimal increment)
    {
        if (widthPercent <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(widthPercent), "The range width must be positive.");
        }
        var factor = 1m + widthPercent / 100m;
        var ranges = new List<SalesRange>();
        var i = 0;
        while (i < sorted.Count)
        {
            var low = sorted[i];
            var mid = RoundTo(low * factor, increment);
            var high = Math.Max(mid, Math.Floor(mid * factor));
            var members = new List<decimal>();
            while (i < sorted.Count && sorted[i] <= high)
            {
                members.Add(sorted[i++]);
            }
            ranges.Add(new SalesRange(ranges.Count + 1, low, mid, high, members));
        }
        return ranges;
    }

    /// <summary>The proposed unit value of merged ranges: the mean of their midpoints weighted by frequency, rounded to the increment.</summary>
    public static decimal ProposedValue(IEnumerable<SalesRange> merged, decimal increment)
    {
        var list = merged.ToList();
        var count = list.Sum(r => r.Frequency);
        return count == 0 ? 0m : RoundTo(list.Sum(r => r.Mid * r.Frequency) / count, increment);
    }
}
