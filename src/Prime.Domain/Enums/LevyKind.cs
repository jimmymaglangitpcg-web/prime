namespace Prime.Domain.Enums;

/// <summary>
/// A levy on real property whose rate the reports multiply assessed values by (docs/analysis/reporting.md §4.4, Q5, Q18).
/// PRIME does not bill (CLAUDE.md §0): the products are report figures only.
/// </summary>
public enum LevyKind
{
    /// <summary>The basic real property tax.</summary>
    Basic = 0,
    /// <summary>The additional levy for the Special Education Fund.</summary>
    SpecialEducationFund = 1,
    /// <summary>The tax on idle lands, where the LGU levies it.</summary>
    IdleLand = 2,
}
