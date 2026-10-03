namespace Prime.Domain.Enums;

/// <summary>The kinds of unit an exemption type can cover.</summary>
[Flags]
public enum ExemptionAppliesTo
{
    Land = 1,
    Building = 2,
    Machinery = 4,
    OtherImprovement = 8,
    All = Land | Building | Machinery | OtherImprovement,
}

/// <summary>The life of an exemption claim (docs/analysis/assessment-listing-exemptions.md §4.1).</summary>
public enum ExemptionStatus
{
    /// <summary>Claimed; no proof yet. The unit is listed as taxable (LGC §206).</summary>
    Claimed = 0,
    /// <summary>Evidence filed; awaiting the decision.</summary>
    ProofFiled = 1,
    Approved = 2,
    Rejected = 3,
    /// <summary>An approved exemption that no longer applies (expiry, change of use or owner).</summary>
    Ended = 4,
}
