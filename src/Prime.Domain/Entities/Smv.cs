using Prime.Domain.Common;
using Prime.Domain.Entities.Reference;
using Prime.Domain.Enums;

namespace Prime.Domain.Entities;

/// <summary>
/// CLAUDE.md §28. One row per Schedule of Market Values — never edited once
/// approved; a new SMV revision is a new row (docs/DATABASE.md §4, the same
/// "revision header" pattern <see cref="TaxDeclaration"/> uses). Rate data
/// itself lives on <see cref="SmvSchedule"/>, one-to-many from here.
///
/// Under RA 12001 an SMV is certified by the Secretary of Finance and takes
/// effect after publication; earlier SMVs were enacted by ordinance
/// (docs/analysis/valuation-foundation.md §4.3). <see cref="Basis"/> says which,
/// and the stage dates record the SMV's history. PRIME's own approval
/// (<see cref="Status"/>) confirms the SMV was <b>entered</b> correctly; it does
/// not stand in for certification.
/// </summary>
public sealed class Smv : AuditableEntity, IVersioned
{
    /// <summary>Row version (xmin): optimistic concurrency, see <see cref="IVersioned"/>.</summary>
    public uint RowVersion { get; set; }

    public SmvBasis Basis { get; set; } = SmvBasis.Ordinance;

    /// <summary>Required for an <see cref="SmvBasis.Ordinance"/> SMV.</summary>
    public string? OrdinanceNumber { get; set; }
    public DateOnly? OrdinanceDate { get; set; }
    /// <summary>The ordinance's approval (an <see cref="SmvBasis.Ordinance"/> SMV).</summary>
    public DateOnly? ApprovalDate { get; set; }

    // Stages of a certified SMV (LAM Bk IV Ch. III). All optional: they record what happened.
    public DateOnly? ProposedOn { get; set; }
    public DateOnly? PublishedForCommentOn { get; set; }
    public DateOnly? ConsultationsHeldOn { get; set; }
    public DateOnly? SubmittedToBlgfOn { get; set; }
    public DateOnly? CertifiedOn { get; set; }
    /// <summary>Required for a <see cref="SmvBasis.Certified"/> SMV: the certification's reference.</summary>
    public string? CertificationReference { get; set; }
    public DateOnly? PublishedOn { get; set; }
    /// <summary>Where it was published (Official Gazette, newspaper).</summary>
    public string? PublicationReference { get; set; }

    public DateOnly EffectivityDate { get; set; }
    public int RevisionYear { get; set; }
    public WorkflowStatus Status { get; set; } = WorkflowStatus.Draft;
    public string? Description { get; set; }

    public Guid? ApprovedBy { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }

    /// <summary>
    /// The municipalities the SMV covers; none = the whole province (Q2). A change of
    /// coverage is a new SMV, so the list is set while the SMV is a draft and never after.
    /// </summary>
    public List<SmvCoverage> Coverage { get; set; } = [];

    /// <summary>
    /// An <see cref="SmvBasis.Amendment"/>: the SMV it amends, an approved SMV that is not itself an amendment, so every
    /// amendment hangs off one SMV (docs/analysis/smv-preparation-general-revision.md §4.7).
    /// </summary>
    public Guid? AmendsSmvId { get; set; }
    public Smv? AmendsSmv { get; set; }
    public SmvAmendmentGround? AmendmentGround { get; set; }

    /// <summary>The SMV whose rows this one's rows belong with: the amended SMV for an amendment, else itself.</summary>
    public Guid FamilyId => AmendsSmvId ?? Id;

    /// <summary>How the SMV is cited: its ordinance number, else its certification reference.</summary>
    public string Reference => OrdinanceNumber ?? CertificationReference ?? string.Empty;
}

/// <summary>One municipality an <see cref="Smv"/> covers.</summary>
public sealed class SmvCoverage : Entity
{
    public Guid SmvId { get; set; }
    public Guid MunicipalityId { get; set; }
    public Municipality? Municipality { get; set; }
}
