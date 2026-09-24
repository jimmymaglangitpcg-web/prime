using Prime.Domain.Common;
using Prime.Domain.Enums;

namespace Prime.Domain.Entities.Workflow;

/// <summary>
/// The configured signatory chain for approving one kind of record
/// (docs/FORMS-REVISION-PLAN.md §4.5), e.g. an assessment's "Appraised by →
/// Recommending approval → Approved by". Step codes, labels and positions
/// are data, so the LAM's chain can be entered without code. With no chain
/// in force the existing two-person maker-checker applies. One chain in
/// force per subject type (a deployment serves one LGU).
/// </summary>
public sealed class ApprovalChain : EffectiveDatedConfiguration
{
    public ApprovalSubjectType SubjectType { get; set; }
    public string Name { get; set; } = string.Empty;
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
}
