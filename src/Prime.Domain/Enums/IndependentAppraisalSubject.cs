namespace Prime.Domain.Enums;

/// <summary>What an independent appraisal values (docs/analysis/valuation-foundation.md §4.7).</summary>
public enum IndependentAppraisalSubject
{
    /// <summary>A land's strips (a special class or special-purpose land); its trees and plants keep their SMV rates.</summary>
    Land = 0,
    /// <summary>A whole building or structure.</summary>
    Building = 1,
    /// <summary>One additional (extra) item of a building.</summary>
    BuildingComponent = 2,
    /// <summary>One machine.</summary>
    Machinery = 3,
}

/// <summary>The approach an independent appraisal took (LAM Bk III p.76).</summary>
public enum AppraisalApproach
{
    Market = 0,
    Income = 1,
    Cost = 2,
}
