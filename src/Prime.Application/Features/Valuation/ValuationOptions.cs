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
}
