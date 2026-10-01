using Prime.Domain.Enums;

namespace Prime.Domain.DomainServices;

/// <summary>
/// The effectivity rules of docs/analysis/valuation-foundation.md §4.2, as pure
/// functions of the date an assessment is made. Which rule a transaction follows
/// and the length of the reassessment window are configuration.
/// </summary>
public static class EffectivityRules
{
    /// <summary>
    /// The effectivity an assessment made on <paramref name="madeOn"/> takes under
    /// <paramref name="rule"/>, or null when the process gives the date
    /// (<see cref="EffectivityRule.Periods"/>, <see cref="EffectivityRule.Fixed"/>).
    /// </summary>
    public static DateOnly? Derive(EffectivityRule rule, DateOnly madeOn) => rule switch
    {
        EffectivityRule.NextJanuary => madeOn.Month == 1 && madeOn.Day == 1 ? madeOn : new DateOnly(madeOn.Year + 1, 1, 1),
        EffectivityRule.NextQuarter => QuarterStart(madeOn).AddMonths(3),
        EffectivityRule.Periods or EffectivityRule.Fixed => null,
        _ => throw new ArgumentOutOfRangeException(nameof(rule), rule, null),
    };

    /// <summary>Whether <paramref name="rule"/> derives the date from the day the assessment is made.</summary>
    public static bool IsDerived(EffectivityRule rule) => rule is EffectivityRule.NextJanuary or EffectivityRule.NextQuarter;

    /// <summary>Whether <paramref name="rule"/> needs the date of the cause (destruction, change of use, …).</summary>
    public static bool NeedsCauseDate(EffectivityRule rule) => rule == EffectivityRule.NextQuarter;

    /// <summary>1–4.</summary>
    public static int Quarter(DateOnly date) => (date.Month - 1) / 3 + 1;

    public static DateOnly QuarterStart(DateOnly date) => new(date.Year, (Quarter(date) - 1) * 3 + 1, 1);

    /// <summary>
    /// Made more than <paramref name="windowDays"/> days after the cause; a made
    /// date on the last day of the window is in time. No window: never late.
    /// </summary>
    public static bool CauseWindowExceeded(DateOnly causeDate, DateOnly madeOn, int? windowDays) =>
        windowDays is { } days && madeOn.DayNumber - causeDate.DayNumber > days;
}
