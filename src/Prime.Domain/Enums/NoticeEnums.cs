namespace Prime.Domain.Enums;

/// <summary>Why a Notice of Assessment is required (LGC §223).</summary>
public enum NoticeReason
{
    /// <summary>Real property assessed for the first time.</summary>
    FirstAssessment = 0,
    AssessmentIncreased = 1,
    AssessmentDecreased = 2,
    // The MRPAAO also sends a notice when the FAAS and TD are updated for a
    // change of declared owner, of the owner's address, or of the property's
    // location (p.168), though the value may be unchanged.
    DeclaredOwnerChanged = 3,
    OwnerAddressChanged = 4,
    LocationChanged = 5,
}

public enum NoticeStatus
{
    Draft = 0,
    Issued = 1,
    Served = 2,
    Cancelled = 3,
}

/// <summary>
/// The modes of delivery LGC §223 allows: personally, by registered mail, or
/// through the assistance of the punong barangay, to the last known address.
/// Electronic service is not listed: no authoritative source establishing it
/// was found (docs/analysis/current-real-property-regulatory-baseline.md Q11).
/// </summary>
public enum NoticeServiceMode
{
    Personal = 0,
    RegisteredMail = 1,
    ThroughPunongBarangay = 2,
    /// <summary>By email to the declarant's address (LAM Annex I-L p.176; records-and-forms.md Q11).</summary>
    Email = 3,
}

/// <summary>Why a Notice of Cancellation is given (LAM 2025 Book III p.89 C; assessment-listing-exemptions.md Q11).</summary>
public enum CancellationNoticeGround
{
    /// <summary>The assessor cancelled the declaration on its own motion (a transaction type flagged so).</summary>
    MotuProprio = 0,
    /// <summary>A reassessment cancelled the assessment declared in the name of someone no longer an owner.</summary>
    PreviousOwner = 1,
}

/// <summary>How a discovery summons ended (assessment-listing-exemptions.md Q10).</summary>
public enum SummonsOutcome
{
    Pending = 0,
    Complied = 1,
    NotComplied = 2,
}
