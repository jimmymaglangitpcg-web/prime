namespace Prime.Domain.DomainServices;

/// <summary>
/// The arithmetic of valuation testing (LAM 2025 Book IV pp.115–116; docs/analysis/smv-preparation-general-revision.md
/// §4.3): accuracy by the median of the value-to-price ratios of a group of sales, uniformity by the coefficient of
/// dispersion — the average absolute deviation of the ratios from their median, as a percent of the median. Benchmarks
/// are configuration; this only computes.
/// </summary>
public static class ValuationTesting
{
    /// <summary>Ratios are kept to four decimals.</summary>
    public const int RatioDecimals = 4;

    /// <summary>Value ÷ price, to four decimals; null when the price is not positive.</summary>
    public static decimal? Ratio(decimal value, decimal price) =>
        price > 0 ? Math.Round(value / price, RatioDecimals, MidpointRounding.AwayFromZero) : null;

    /// <summary>
    /// The count, median ratio and coefficient of dispersion (percent, two decimals) of a group of ratios. The median of
    /// an even count is the mean of the two middle ratios. The CoD is null for an empty group or a zero median.
    /// </summary>
    public static (int Count, decimal? Median, decimal? CoefficientOfDispersion) Statistics(IEnumerable<decimal> ratios)
    {
        var sorted = ratios.Order().ToList();
        if (sorted.Count == 0)
        {
            return (0, null, null);
        }
        var mid = sorted.Count / 2;
        var median = sorted.Count % 2 == 1 ? sorted[mid] : Math.Round((sorted[mid - 1] + sorted[mid]) / 2, RatioDecimals, MidpointRounding.AwayFromZero);
        if (median == 0)
        {
            return (sorted.Count, median, null);
        }
        var averageDeviation = sorted.Sum(r => Math.Abs(r - median)) / sorted.Count;
        return (sorted.Count, median, Math.Round(averageDeviation / median * 100m, 2, MidpointRounding.AwayFromZero));
    }

    /// <summary>Square metres in a hectare: converts a sale's area to the unit its unit value is given per.</summary>
    public const decimal SquareMetresPerHectare = 10_000m;
}
