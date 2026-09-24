namespace Prime.Domain.Enums;

/// <summary>
/// In whose name, and in what capacity, real property is listed, valued and
/// assessed (LGC §§204–205; docs/FORMS-REVISION-PLAN.md §5 A4). These are
/// statutory capacities, so they are fixed here; their printed labels and
/// any LAM-specific sub-kinds come later as configuration.
/// DOMAIN VERIFICATION REQUIRED: how liability is shared among non-owner
/// parties, and the LAM's treatment of claimants of untitled land.
/// </summary>
public enum PropertyPartyRole
{
    /// <summary>Owner or co-owner (§205(a),(c)). Only owners' shares count toward the 100% ownership total.</summary>
    Owner = 0,

    /// <summary>Administrator, e.g. of an estate (§205(a)–(b)).</summary>
    Administrator = 1,

    /// <summary>Anyone else having legal interest in the property (§205(a)).</summary>
    LegalInterestHolder = 2,

    /// <summary>Grantee or possessor of government property with beneficial use (§205(d)).</summary>
    BeneficialUser = 3,

    /// <summary>A claimant to (e.g. untitled) property.</summary>
    Claimant = 4,

    /// <summary>
    /// Declared by the assessor against an unknown owner (§204). Has no
    /// taxpayer; ended automatically when an owner is identified.
    /// </summary>
    UnknownOwner = 5,
}
