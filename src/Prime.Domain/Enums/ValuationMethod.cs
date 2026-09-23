namespace Prime.Domain.Enums;

/// <summary>
/// Which calculation algorithm <see cref="DomainServices.ValuationCalculator"/>
/// used. Fixed by code capability (only these two are implemented), not an
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
}
