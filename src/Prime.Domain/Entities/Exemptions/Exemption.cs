using Prime.Domain.Common;
using Prime.Domain.Entities.Reference;
using Prime.Domain.Enums;

namespace Prime.Domain.Entities.Exemptions;

/// <summary>
/// A kind of exemption from real property tax, with its legal basis (LAM 2025 Book III Ch. V; LGC §234 and other laws;
/// docs/analysis/assessment-listing-exemptions.md §4.1). Configuration, versioned by <see cref="Code"/> and approved by a
/// second user. The repository ships DEMO types only; the province's list is content (CLAUDE.md §118).
/// </summary>
public sealed class ExemptionType : EffectiveDatedConfiguration
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>The kinds of unit the exemption can cover.</summary>
    public ExemptionAppliesTo AppliesTo { get; set; } = ExemptionAppliesTo.All;

    /// <summary>Whether the claimant must file documentary evidence (LGC §206). Yes unless the law says otherwise.</summary>
    public bool RequiresProof { get; set; } = true;

    /// <summary>The highest assessed value the exemption covers, where the law sets one (Q14); null: no ceiling.</summary>
    public decimal? AssessedValueCeiling { get; set; }
}

/// <summary>
/// A claim of exemption for one unit (RPU), or for the part of it in one actual use (Q1, a line of its assessment).
/// Until approved the unit stays taxable (LGC §206). Never deleted: a claim is rejected or ended, with a reason.
/// </summary>
public sealed class PropertyExemption : AuditableEntity
{
    public Guid PropertyId { get; set; }
    public PropertyEntity? Property { get; set; }
    public Guid RpuId { get; set; }
    public RealPropertyUnit? Rpu { get; set; }
    public Guid ExemptionTypeId { get; set; }
    public ExemptionType? ExemptionType { get; set; }

    /// <summary>The actual use of the exempt part; null: the whole unit.</summary>
    public Guid? ActualUseId { get; set; }
    public ActualUse? ActualUse { get; set; }
    public string? PortionDescription { get; set; }

    /// <summary>Who claims the exemption (an owner or administrator), when recorded.</summary>
    public Guid? ClaimantTaxpayerId { get; set; }
    public Taxpayer? ClaimantTaxpayer { get; set; }

    /// <summary>The date of the declaration the claim is made on; the proof period runs from it.</summary>
    public DateOnly ClaimedOn { get; set; }
    public DateOnly ProofDueDate { get; set; }

    /// <summary>The instrument the exemption rests on: CDA registration, CADT, lease, charter …</summary>
    public string? Reference { get; set; }
    public string? Remarks { get; set; }

    public ExemptionStatus Status { get; set; } = ExemptionStatus.Claimed;
    public DateOnly? ProofFiledOn { get; set; }

    /// <summary>Approved: from when, and until when (null: open).</summary>
    public DateOnly? EffectiveDate { get; set; }
    public DateOnly? ExpiryDate { get; set; }

    public Guid? DecidedBy { get; set; }
    public DateTimeOffset? DecidedAt { get; set; }
    public string? DecisionRemarks { get; set; }

    public DateOnly? EndedOn { get; set; }
    public string? EndReason { get; set; }

    /// <summary>
    /// The draft reassessment opened when this exemption was approved or ended after the unit's assessment was made
    /// (Q3): same values, lines re-marked, going through the normal workflow to a replacing TD. Null if none was needed.
    /// </summary>
    public Guid? ReassessmentId { get; set; }

    public List<ExemptionEvidence> Evidence { get; set; } = [];
}

/// <summary>
/// One document filed as proof (LAM Book III p.100: charters, titles, by-laws, contracts, affidavits, certifications …).
/// Recorded by reference; attaching the file waits for the documents module (CLAUDE.md §59).
/// </summary>
public sealed class ExemptionEvidence : Entity
{
    public Guid PropertyExemptionId { get; set; }
    public int Sequence { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? ReferenceNumber { get; set; }
    public DateOnly? DocumentDate { get; set; }
    public DateOnly ReceivedOn { get; set; }
    public Guid? ReceivedBy { get; set; }
}
