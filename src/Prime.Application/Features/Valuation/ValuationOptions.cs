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
    /// LGC §222: back taxes reach "not ... more than ten (10) years prior to the date of initial assessment".
    /// Null: back taxes cannot be computed (docs/analysis/valuation-foundation.md §4.8).
    /// </summary>
    public int? BackTaxYearsLimit { get; set; }

    /// <summary>Citation for <see cref="BackTaxYearsLimit"/>; required when it is set.</summary>
    public string? BackTaxYearsLimitLegalBasis { get; set; }

    /// <summary>
    /// Which rules value a building in each back-tax period (Q14; LAM Bk III p.78 vs p.79): the current ones
    /// (the default: the current construction cost for every period) or the period's own.
    /// </summary>
    public BackTaxRules BackTaxBuildingRules { get; set; } = BackTaxRules.Current;

    /// <summary>Likewise for machinery; by period by default (its age and indices change).</summary>
    public BackTaxRules BackTaxMachineryRules { get; set; } = BackTaxRules.ByPeriod;

    /// <summary>
    /// The step each row's market value is rounded to (e.g. 10 = the nearest ten, as the LAM's
    /// FAAS annexes show), half away from zero. Null: no rounding — the default until the
    /// office confirms the rule ([C5]; docs/analysis/valuation-foundation.md §4.4).
    /// </summary>
    public decimal? MarketValueRoundingStep { get; set; }

    /// <summary>Citation for <see cref="MarketValueRoundingStep"/>; required when a step is set.</summary>
    public string? MarketValueRoundingLegalBasis { get; set; }
}

/// <summary>Which rules value a unit in each back-tax period (docs/analysis/valuation-foundation.md §4.8, Q14). Land is always by period.</summary>
public enum BackTaxRules
{
    /// <summary>The SMV, costs and tables in force at each period's start.</summary>
    ByPeriod = 0,
    /// <summary>Those in force at the start of the current (last) period, for every period.</summary>
    Current = 1,
}
