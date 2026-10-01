using Prime.Domain.Common;
using Prime.Domain.Enums;

namespace Prime.Domain.Entities.Workflow;

/// <summary>
/// The configured signatory chain for approving one kind of record
/// (docs/FORMS-REVISION-PLAN.md §4.5), e.g. an assessment's "Appraised by →
/// Recommending approval → Approved by". Step codes, labels and positions
/// are data, so the LAM's chain can be entered without code. With no chain
/// in force the existing two-person maker-checker applies. One chain in
/// force per subject type and office: a chain with no office is the
/// provincial default, used for a record whose office has none of its own
/// (docs/analysis/province-wide-operation.md §3.4).
/// </summary>
public sealed class ApprovalChain : EffectiveDatedConfiguration
{
    public ApprovalSubjectType SubjectType { get; set; }
    public string Name { get; set; } = string.Empty;
    /// <summary>The municipal office this chain is for; null for the provincial default.</summary>
    public Guid? OfficeId { get; set; }
    public Prime.Domain.Entities.Offices.Office? Office { get; set; }
    public List<ApprovalChainStep> Steps { get; set; } = [];
}

public sealed class ApprovalChainStep : Entity
{
    public Guid ApprovalChainId { get; set; }
    public int Sequence { get; set; }
    /// <summary>Stable code, e.g. RECOMMENDED_BY.</summary>
    public string StepCode { get; set; } = string.Empty;
    /// <summary>Printed label, e.g. "Recommending Approval".</summary>
    public string Label { get; set; } = string.Empty;
    /// <summary>Printed under the signer's name, e.g. "Municipal Assessor". Optional.</summary>
    public string? SignatoryPosition { get; set; }

    /// <summary>Whose staff signs this step (§3.4): anyone (as before offices), the preparing municipal office, or the provincial office.</summary>
    public ApprovalSigner SignerOffice { get; set; } = ApprovalSigner.Any;

    /// <summary>A role code the signer must hold in their office, e.g. ASSESSOR. Optional.</summary>
    public string? RequiredRole { get; set; }

    /// <summary>
    /// The step that makes the record final (the last one). When the province
    /// signs it and a delegation to the preparing office is in force on the
    /// signing date, that office's ASSESSOR signs it instead.
    /// </summary>
    public bool IsFinalApproval { get; set; }
}

/// <summary>
/// One completed approval step on one record — immutable. Freezes the
/// step's label and position and the signer's name, so a form printed later
/// shows who signed and in what capacity at the time.
/// </summary>
public sealed class ApprovalRecord : Entity
{
    public ApprovalSubjectType SubjectType { get; set; }
    public Guid SubjectId { get; set; }
    public Guid ApprovalChainId { get; set; }
    public int StepSequence { get; set; }
    public string StepCode { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string? SignatoryPosition { get; set; }
    public Guid? UserId { get; set; }
    public string SignatoryName { get; set; } = string.Empty;
    public DateTimeOffset SignedAt { get; set; }
    public string? Remarks { get; set; }

    /// <summary>The signer's office when they signed; null for a province-wide or unassigned signer.</summary>
    public Guid? SignerOfficeId { get; set; }

    /// <summary>The delegation the step was signed under, if any (§3.4).</summary>
    public Guid? DelegationId { get; set; }

    /// <summary>The delegation as printed, frozen: instrument, date and delegating official.</summary>
    public string? UnderDelegation { get; set; }
}
