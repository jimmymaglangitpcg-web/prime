using Prime.Domain.Entities;
using Prime.Domain.Enums;

namespace Prime.Domain.DomainServices;

/// <summary>The depreciation percent a table gives an age, or why it gives none.</summary>
public sealed record DepreciationOutcome(decimal Percent, bool Capped, string? Problem)
{
    public static DepreciationOutcome Of(decimal percent, bool capped) => new(percent, capped, null);
    public static DepreciationOutcome Refuse(string problem) => new(0m, false, problem);
}

/// <summary>
/// A building's age and the depreciation an SMV's table gives it (docs/analysis/valuation-foundation.md
/// §4.5, Q8, Q9). Pure: every band and percent comes from the table.
/// </summary>
public static class BuildingDepreciation
{
    /// <summary>
    /// Age on the valuation date (Q9): the valuation year minus the year the building was completed,
    /// else constructed, else occupied; never below zero. Null when none of those is recorded.
    /// </summary>
    public static int? Age(Building building, DateOnly valuationDate)
    {
        var from = building.YearCompleted ?? building.YearConstructed ?? building.DateConstructed?.Year ?? building.DateOccupied?.Year;
        return from is { } year ? Math.Max(0, valuationDate.Year - year) : null;
    }

    /// <summary>
    /// The depreciation percent for <paramref name="age"/>:
    /// <list type="bullet">
    /// <item><see cref="DepreciationReading.Cumulative"/>: the percent of the band holding the age;</item>
    /// <item><see cref="DepreciationReading.YearlyWithinBand"/>: each band's rate × the years of the age
    /// falling in it, added up (band "1–5" holds years 1 to 5).</item>
    /// </list>
    /// An age below the first band is not depreciated. An age beyond the last band is refused: the
    /// table does not say. The result never exceeds 100 minus the minimum remaining percent.
    /// </summary>
    public static DepreciationOutcome Percent(DepreciationReading reading, IReadOnlyList<SmvDepreciationRow> rows, decimal minimumRemainingPercent, int age)
    {
        var bands = rows.OrderBy(r => r.FromAge).ToList();
        if (bands.Count == 0)
        {
            return DepreciationOutcome.Refuse("The depreciation table has no age bands.");
        }
        if (bands[^1].ToAge is { } last && age > last)
        {
            return DepreciationOutcome.Refuse($"The depreciation table stops at {last} years; the building is {age} years old.");
        }
        decimal percent;
        if (reading == DepreciationReading.Cumulative)
        {
            percent = bands.LastOrDefault(b => b.FromAge <= age)?.Percent ?? 0m;
        }
        else
        {
            percent = 0m;
            foreach (var band in bands)
            {
                // Years of the age inside [FromAge, ToAge]: year n counts when FromAge <= n <= min(ToAge, age), counting years from 1.
                var first = Math.Max(band.FromAge, 1);
                var lastYear = Math.Min(band.ToAge ?? age, age);
                if (lastYear >= first)
                {
                    percent += band.Percent * (lastYear - first + 1);
                }
            }
        }
        var cap = 100m - minimumRemainingPercent;
        return percent > cap ? DepreciationOutcome.Of(cap, true) : DepreciationOutcome.Of(percent, false);
    }

    /// <summary>Why a table's bands are not usable, or null: each band from ≥ 0, ending at or after it starts, contiguous, no overlap.</summary>
    public static string? BandsProblem(IReadOnlyList<(int FromAge, int? ToAge, decimal Percent)> rows)
    {
        if (rows.Count == 0)
        {
            return "A depreciation table has at least one age band.";
        }
        if (rows.Any(r => r.FromAge < 0 || r.ToAge < r.FromAge || r.Percent is < 0m or > 100m))
        {
            return "Each age band starts at 0 or later, ends at or after its start, and has a percent from 0 to 100.";
        }
        var ordered = rows.OrderBy(r => r.FromAge).ToList();
        for (var i = 1; i < ordered.Count; i++)
        {
            if (ordered[i - 1].ToAge is not { } previousEnd)
            {
                return "Only the last age band can be open-ended.";
            }
            if (ordered[i].FromAge != previousEnd + 1)
            {
                return $"Age bands must follow each other without gap or overlap: a band ends at {previousEnd}, the next starts at {ordered[i].FromAge}.";
            }
        }
        return null;
    }
}
