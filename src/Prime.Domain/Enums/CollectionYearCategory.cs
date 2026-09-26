namespace Prime.Domain.Enums;

/// <summary>
/// How a payment's tax year relates to the year it was paid in — collections
/// are reported, and coded to revenue accounts, by this (CLAUDE.md §41
/// "tax-year collection"; docs/analysis/collection.md §3).
/// </summary>
public enum CollectionYearCategory
{
    /// <summary>The tax year is the year of payment.</summary>
    Current = 0,

    /// <summary>A tax year before the year of payment.</summary>
    Prior = 1,

    /// <summary>A tax year after the year of payment.</summary>
    Advance = 2,
}
