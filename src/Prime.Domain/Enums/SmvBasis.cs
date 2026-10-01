namespace Prime.Domain.Enums;

/// <summary>How an SMV came into force (docs/analysis/valuation-foundation.md §4.3).</summary>
public enum SmvBasis
{
    /// <summary>Enacted by a local ordinance (before RA 12001).</summary>
    Ordinance = 0,

    /// <summary>Certified by the Secretary of Finance and published (RA 12001).</summary>
    Certified = 1,
}
