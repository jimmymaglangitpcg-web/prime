namespace Prime.Domain.Enums;

/// <summary>
/// Which calculation algorithm <see cref="DomainServices.ValuationCalculator"/>
/// used. Fixed by code capability (only these are implemented), not an
/// LGU-configurable reference table — the same "fixed-by-code vs.
/// LGU-configurable" split already applied to <see cref="WorkflowStatus"/>
/// vs. the <c>Reference/</c> lookup tables (docs/DOMAIN-MODEL.md §3.10).
/// CLAUDE.md §30 lists more methods (replacement cost, depreciation,
/// location adjustment, "other legally applicable methods") than are
/// implemented here — new values are added as new methods are built, never
/// invented ahead of an actual calculation.
/// </summary>
public enum ValuationMethod
{
    SmvBased = 0,
    ReplacementCost = 1,

    /// <summary>Brand-new machinery valued at acquisition cost (LGC §224(a)).</summary>
    AcquisitionCost = 2,

    /// <summary>
    /// Machinery not brand-new: replacement cost derived from the acquisition cost, the exchange
    /// rates and a price index, then depreciated (LAM Bk III pp.73–75; valuation-foundation.md §4.6).
    /// <see cref="ReplacementCost"/> stays the method for an entered replacement cost.
    /// </summary>
    DerivedReplacementCost = 3,

    /// <summary>A value the appraiser determined outside the SMV, by the market, income or cost approach (valuation-foundation.md §4.7).</summary>
    IndependentAppraisal = 4,
}
