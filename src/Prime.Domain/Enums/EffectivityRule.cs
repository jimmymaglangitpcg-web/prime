namespace Prime.Domain.Enums;

/// <summary>
/// How an assessment's effectivity date is found (docs/analysis/valuation-foundation.md
/// §4.2). The kinds are code; which transaction type uses which, and on what legal
/// basis, is configuration on <c>TransactionType</c>.
/// </summary>
public enum EffectivityRule
{
    /// <summary>Made on 1 January: that day; otherwise 1 January of the next year (LGC §221; LAM Bk III p.85).</summary>
    NextJanuary = 0,

    /// <summary>The first day of the quarter after the one it is made in (reassessment; LAM Bk III p.85 §6).</summary>
    NextQuarter = 1,

    /// <summary>Each period's own start, given by the back-tax process (step L1-8).</summary>
    Periods = 2,

    /// <summary>A date given by the process, e.g. a general revision's effectivity.</summary>
    Fixed = 3,
}
