namespace Prime.Application.Features.Valuation;

/// <summary>
/// "Valuation" configuration section — legal parameters the valuation
/// engine applies but does not own (CLAUDE.md §7). Each value carries its
/// legal basis next to it in configuration, and every valuation records the
/// value it used in its breakdown, so earlier valuations stay reproducible
/// after a change.
/// </summary>
public sealed class ValuationOptions
{
    public const string SectionName = "Valuation";

    /// <summary>
    /// LGC §225 proviso: machinery's remaining value is "not less than twenty
    /// percent (20%) of such original, replacement, or reproduction cost".
    /// Required; 0–100.
    /// </summary>
    public decimal? MachineryMinimumRemainingValuePercent { get; set; }

    /// <summary>Citation for <see cref="MachineryMinimumRemainingValuePercent"/>, e.g. "RA 7160 §225". Required.</summary>
    public string? MachineryMinimumRemainingValueLegalBasis { get; set; }

    /// <summary>
    /// LGC §225: machinery depreciation "not exceeding five percent (5%) ... for each year of use",
    /// applied as a cap on years of use ÷ economic life in the derived method (Q11). Null: machinery
    /// cannot be valued by the derived method.
    /// </summary>
    public decimal? MachineryMaximumYearlyDepreciationPercent { get; set; }

    /// <summary>Citation for <see cref="MachineryMaximumYearlyDepreciationPercent"/>; required when it is set.</summary>
    public string? MachineryMaximumYearlyDepreciationLegalBasis { get; set; }

    /// <summary>
    /// The step each row's market value is rounded to (e.g. 10 = the nearest ten, as the LAM's
    /// FAAS annexes show), half away from zero. Null: no rounding — the default until the
    /// office confirms the rule ([C5]; docs/analysis/valuation-foundation.md §4.4).
    /// </summary>
    public decimal? MarketValueRoundingStep { get; set; }

    /// <summary>Citation for <see cref="MarketValueRoundingStep"/>; required when a step is set.</summary>
    public string? MarketValueRoundingLegalBasis { get; set; }
}
