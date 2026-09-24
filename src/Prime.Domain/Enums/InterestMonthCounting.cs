namespace Prime.Domain.Enums;

/// <summary>
/// docs/BILLING.md §3.5. How a partial month of delinquency counts toward
/// interest — legally significant and not settled, so it is configured per
/// rule rather than assumed (DOMAIN VERIFICATION REQUIRED).
/// </summary>
public enum InterestMonthCounting
{
    /// <summary>Any started month counts as a full month.</summary>
    FractionCountsAsFullMonth = 0,

    /// <summary>Only fully elapsed months count.</summary>
    CompletedMonthsOnly = 1,
}
