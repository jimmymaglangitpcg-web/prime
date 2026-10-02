using Prime.Domain.Common;
using Prime.Domain.Enums;

namespace Prime.Domain.Entities;

/// <summary>
/// A value the appraiser determined outside the SMV (LAM Bk III pp.71–72, p.76;
/// docs/analysis/valuation-foundation.md §4.7): for a land of a special class, a building or
/// structure, an extra item or a machine the SMV does not cover, or special-purpose property. It
/// records the approach, the basis, the evidence and any named inputs. PRIME does not compute an
/// income or cost approach (Q13); the assessment's maker-checker approval is the review.
/// A newer appraisal of the same subject replaces it as current; none is ever deleted.
/// </summary>
public sealed class IndependentAppraisal : AuditableEntity
{
    public Guid RpuId { get; set; }
    public RealPropertyUnit? Rpu { get; set; }
    public IndependentAppraisalSubject Subject { get; set; }
    /// <summary>The land, building, building component (an additional item) or machine appraised.</summary>
    public Guid SubjectId { get; set; }
    public AppraisalApproach Approach { get; set; }
    public decimal Value { get; set; }
    public DateOnly AppraisedOn { get; set; }
    /// <summary>Why this approach and value, e.g. the comparable sales or the income capitalised.</summary>
    public string Basis { get; set; } = string.Empty;
    /// <summary>Where the evidence is: document references, file numbers.</summary>
    public string Evidence { get; set; } = string.Empty;
    public bool IsCurrent { get; set; } = true;
    /// <summary>Why it was withdrawn or replaced, when it was.</summary>
    public string? EndReason { get; set; }
    public List<IndependentAppraisalInput> Inputs { get; set; } = [];
}

/// <summary>A named figure the appraisal used (e.g. gross income, capitalisation rate), kept in the breakdown.</summary>
public sealed class IndependentAppraisalInput : Entity
{
    public Guid IndependentAppraisalId { get; set; }
    public int Sequence { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Value { get; set; }
    public string? Unit { get; set; }
}
