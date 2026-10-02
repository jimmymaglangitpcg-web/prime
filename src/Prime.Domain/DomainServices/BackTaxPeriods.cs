namespace Prime.Domain.DomainServices;

/// <summary>One back-tax period: valued and assessed as of <see cref="Start"/>, until the day before the next one (null: still current).</summary>
public sealed record BackTaxPeriodSpan(int Sequence, DateOnly Start, DateOnly? End);

/// <summary>
/// Back taxes of a property declared for the first time or discovered (LAM Bk III pp.78–80; LGC §222;
/// docs/analysis/valuation-foundation.md §4.8). Pure: the dates come from the SMVs and the limit from
/// configuration.
/// </summary>
public static class BackTaxPeriods
{
    /// <summary>
    /// Why back taxes from <paramref name="fromYear"/> cannot be charged on a property first assessed in
    /// <paramref name="initialYear"/>, or null: the start is not after the initial year and not more than
    /// <paramref name="yearsLimit"/> years before it.
    /// </summary>
    public static string? StartProblem(int fromYear, int initialYear, int yearsLimit) =>
        fromYear > initialYear ? $"The back taxes cannot start ({fromYear}) after the year of initial assessment ({initialYear})."
        : initialYear - fromYear > yearsLimit
            ? $"Back taxes reach at most {yearsLimit} years before the year of initial assessment ({initialYear}): from {initialYear - yearsLimit}, not {fromYear}."
            : null;

    /// <summary>
    /// The periods from 1 January of <paramref name="fromYear"/>, cut at each SMV effectivity date after it and
    /// not after <paramref name="until"/>: each period is valued under the SMV in force at its start, and the
    /// last one stays current (Q15).
    /// </summary>
    public static IReadOnlyList<BackTaxPeriodSpan> Split(int fromYear, DateOnly until, IEnumerable<DateOnly> smvEffectivityDates)
    {
        var start = new DateOnly(fromYear, 1, 1);
        var starts = smvEffectivityDates.Where(d => d > start && d <= until).Distinct().Order().Prepend(start).ToList();
        return starts.Select((s, i) => new BackTaxPeriodSpan(i + 1, s, i + 1 < starts.Count ? starts[i + 1].AddDays(-1) : null)).ToList();
    }
}
