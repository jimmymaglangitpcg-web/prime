namespace Prime.Domain.Enums;

/// <summary>
/// How an SMV's depreciation table reads (docs/analysis/valuation-foundation.md §4.5, Q8 [C3]):
/// the province's table decides which.
/// </summary>
public enum DepreciationReading
{
    /// <summary>Each band's percent is the total depreciation for an age within it.</summary>
    Cumulative = 0,
    /// <summary>Each band's percent is a rate per year of age within it; the years add up band by band.</summary>
    YearlyWithinBand = 1,
}
