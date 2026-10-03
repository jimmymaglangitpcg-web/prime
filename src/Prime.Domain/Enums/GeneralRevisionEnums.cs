namespace Prime.Domain.Enums;

/// <summary>Where a general revision programme stands (docs/analysis/smv-preparation-general-revision.md §4.6).</summary>
public enum GeneralRevisionStatus
{
    Planned = 0,
    InProgress = 1,
    Completed = 2,
    Cancelled = 3,
}

/// <summary>What one run of a programme does.</summary>
public enum GeneralRevisionRunMode
{
    /// <summary>Compiles the units in scope into items, with their posted assessment (GRI 3–5).</summary>
    Compile = 0,
    /// <summary>Values and assesses the items as of the revision's effectivity (GRI 12–13).</summary>
    Value = 1,
    /// <summary>Submits the items' Draft assessments for review.</summary>
    Submit = 2,
    /// <summary>Signs the next approval step of each assessment pending review, as the user who started the run (GRI 14; Q14).</summary>
    Approve = 3,
    /// <summary>Returns the assessments pending review, with the run's reason.</summary>
    Reject = 4,
    /// <summary>Posts the approved assessments in PIN order; each prepares its Draft Tax Declaration, so TD numbers follow the tax map (GRI 15).</summary>
    Post = 5,
    /// <summary>Submits the Draft Tax Declarations declaring the items' assessments.</summary>
    SubmitTaxDeclarations = 6,
    /// <summary>Signs the next approval step of each of those Tax Declarations pending review.</summary>
    ApproveTaxDeclarations = 7,
    /// <summary>Drafts the Notices of Assessment the posted items need (LGC §223): one combined notice per sole declared owner, else one per unit.</summary>
    GenerateNotices = 8,
    /// <summary>Issues (and numbers) the items' draft notices.</summary>
    IssueNotices = 9,
}

/// <summary>Where one unit stands in the revision's valuation.</summary>
public enum GeneralRevisionItemStatus
{
    /// <summary>Compiled, not yet valued (or reset for another run).</summary>
    Pending = 0,
    /// <summary>Valued and assessed: a Draft assessment awaits review.</summary>
    Assessed = 1,
    /// <summary>The run could not value or assess it; the reason is recorded.</summary>
    Failed = 2,
    /// <summary>Taken out of the revision with a reason (e.g. retired since compiling, or appraised outside it); not valued, not counted by the gates.</summary>
    Excluded = 3,
}

/// <summary>
/// A condition of a general revision PRIME checks itself (docs/analysis/smv-preparation-general-revision.md §4.6, Q13). A step
/// of the checklist template may name one; the step is then done when the condition holds, not when someone ticks it.
/// </summary>
public enum GeneralRevisionGate
{
    /// <summary>The units in scope are compiled.</summary>
    Compiled = 0,
    /// <summary>Every unit is valued and assessed (none pending or failed).</summary>
    Valued = 1,
    /// <summary>Every unit's assessment is approved (or posted).</summary>
    Approved = 2,
    Posted = 3,
    /// <summary>Every posted unit is declared by an approved Tax Declaration.</summary>
    TaxDeclarationsApproved = 4,
    /// <summary>Every notice the units need (LGC §223) is served.</summary>
    NoticesServed = 5,
    /// <summary>The waiting period after the latest receipt has passed in every city/municipality (GRI 17).</summary>
    RollWaitElapsed = 6,
    /// <summary>A taxable assessment roll run of the revision exists for every barangay with units.</summary>
    AssessmentRollRun = 7,
    /// <summary>The revision's Ownership Record Forms are run.</summary>
    OwnershipRecordsRun = 8,
    /// <summary>The completion report (GRI 19) is issued.</summary>
    CompletionReportIssued = 9,
}

/// <summary>Why a general revision is suspended (LAM 2025 Book IV p.125).</summary>
public enum GeneralRevisionSuspensionKind
{
    /// <summary>A local state of calamity declared by the local chief executive: a set number of days, extendable.</summary>
    LocalCalamity = 0,
    /// <summary>An extension of a calamity suspension recommended by the BLGF to the Secretary of Finance.</summary>
    Extension = 1,
    /// <summary>A national emergency declared by the President: until lifted.</summary>
    NationalEmergency = 2,
}
