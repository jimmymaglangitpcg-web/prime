namespace Prime.Domain.Enums;

/// <summary>docs/BILLING.md §3.7. Where a cap on the tax increase from a new SMV comes from.</summary>
public enum TaxIncreaseCapBasis
{
    /// <summary>The national first-year cap (RA 12001 §29 ¶3; IRR §55). One year, per tax type.</summary>
    StatutoryFirstYear = 0,

    /// <summary>A cap the LGU enacts by ordinance for the years after the first (RA 12001 §29 ¶3 proviso).</summary>
    LocalOrdinance = 1,
}
