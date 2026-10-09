using Prime.Domain.Common;
using Prime.Domain.Entities.Reference;
using Prime.Domain.Enums;

namespace Prime.Domain.Entities;

/// <summary>
/// CLAUDE.md §32. Applies an effective-dated <see cref="AssessmentLevel"/>
/// to a <see cref="Entities.Valuation"/>'s <c>ComputedMarketValue</c> to
/// produce <see cref="AssessedValue"/> — never recomputes the market value
/// itself (CLAUDE.md Rule 9; <see cref="ValuationId"/> is the single source
/// of truth for that). <see cref="AssessmentLevelId"/> and
/// <see cref="AssessmentPercentage"/> are both frozen at computation time:
/// <see cref="AssessmentLevel"/> rows are versioned/effective-dated, so a
/// historical Assessment must not silently change meaning if the rate row
/// it used is later superseded. Never overwritten — a new assessment year
/// or a reassessment is a new row referencing the prior one via
/// <see cref="PreviousAssessmentId"/>, the same supersession-chain pattern
/// as <see cref="TaxDeclaration"/>. <see cref="RevisionReference"/> is set
/// when this row was produced by a General Revision batch.
/// </summary>
public sealed class Assessment : AuditableEntity, IVersioned
{
    /// <summary>Row version (xmin): optimistic concurrency, see <see cref="IVersioned"/>.</summary>
    public uint RowVersion { get; set; }

    public Guid RpuId { get; set; }
    public RealPropertyUnit? Rpu { get; set; }
    public Guid PropertyId { get; set; }
    public PropertyEntity? Property { get; set; }

    public Guid ValuationId { get; set; }
    public Valuation? Valuation { get; set; }

    public int AssessmentYear { get; set; }
    public decimal MarketValue { get; set; }

    /// <summary>
    /// The level applied when the assessment has one line; null for a
    /// mixed-use assessment, whose <see cref="Lines"/> carry a level each.
    /// </summary>
    public Guid? AssessmentLevelId { get; set; }
    public AssessmentLevel? AssessmentLevel { get; set; }
    public decimal? AssessmentPercentage { get; set; }
    /// <summary>Σ of the lines' assessed values.</summary>
    public decimal AssessedValue { get; set; }

    /// <summary>The FAAS "Property Assessment" rows (docs/analysis/mrpaao-forms-model.md §8.2).</summary>
    public List<AssessmentLine> Lines { get; set; } = [];

    public WorkflowStatus Status { get; set; } = WorkflowStatus.Draft;
    public DateOnly EffectiveDate { get; set; }
    /// <summary>Of <see cref="EffectiveDate"/>; a stored column computed by the database, never typed.</summary>
    public int EffectivityYear { get; private set; }
    /// <summary>1–4, of <see cref="EffectiveDate"/>; computed by the database.</summary>
    public int EffectivityQuarter { get; private set; }

    /// <summary>
    /// The transaction type the assessment was made under, and its code and
    /// effectivity rule frozen from that version (docs/analysis/valuation-foundation.md §4.2).
    /// Null for assessments made before step L1-2 or without a type.
    /// </summary>
    public Guid? TransactionTypeId { get; set; }
    public string? TransactionCode { get; set; }
    public EffectivityRule? EffectivityRule { get; set; }
    /// <summary>The event a reassessment answers (destruction, change of use, …), where the rule needs one.</summary>
    public DateOnly? CauseDate { get; set; }
    /// <summary>The reassessment window in force when it was made, frozen with <see cref="CauseWindowExceeded"/>.</summary>
    public int? CauseWindowDays { get; set; }
    /// <summary>Made later than the window after the cause: a warning on the record, not a refusal (Q4).</summary>
    public bool CauseWindowExceeded { get; set; }
    /// <summary>
    /// The date the assessment was made, i.e. finally approved (Q3). Null while
    /// it is a draft, and for assessments approved before step L1-2.
    /// </summary>
    public DateOnly? MadeOn { get; set; }
    /// <summary>Why the effectivity differs from the one its rule gives. Set only with an override.</summary>
    public string? EffectivityOverrideReason { get; set; }

    public Guid? PreviousAssessmentId { get; set; }
    public Assessment? PreviousAssessment { get; set; }

    /// <summary>
    /// Set when produced by a General Revision batch (a plain scalar Guid
    /// here, not yet an FK — <c>GeneralRevisionJob</c> doesn't exist until
    /// Phase 6 checkpoint C adds it; the relationship is wired up then).
    /// </summary>
    public Guid? RevisionReference { get; set; }

    public string? Remarks { get; set; }

    /// <summary>
    /// The number of this assessment's appraisal record (FAAS), assigned when
    /// its approval completes if a FAAS numbering scheme is in force
    /// (docs/FORMS-REVISION-PLAN.md A7). Null otherwise — never typed by hand.
    /// </summary>
    public string? FaasNumber { get; set; }
    public Guid? ApprovedBy { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }

    /// <summary>
    /// When and by whom it was posted — entered in the Record of Assessment
    /// (MRPAAO Att. 1–3 "Date of Entry in the Record of Assessment … By";
    /// docs/analysis/mrpaao-forms-model.md §12). Null for assessments posted
    /// before the stamp existed.
    /// </summary>
    public DateTimeOffset? PostedAt { get; set; }
    public Guid? PostedBy { get; set; }
}

/// <summary>
/// One FAAS "Property Assessment" row: the valuation lines of one
/// classification and actual use, their market value, the level applied
/// (frozen, like the assessment's) and the assessed value.
/// </summary>
public sealed class AssessmentLine : Entity
{
    public Guid AssessmentId { get; set; }
    public int Sequence { get; set; }

    public Guid ClassificationId { get; set; }
    public Classification? Classification { get; set; }
    public Guid ActualUseId { get; set; }
    public ActualUse? ActualUse { get; set; }
    public Guid PropertyTypeId { get; set; }
    public PropertyType? PropertyType { get; set; }

    public decimal MarketValue { get; set; }
    public Guid AssessmentLevelId { get; set; }
    public AssessmentLevel? AssessmentLevel { get; set; }
    public decimal AssessmentPercentage { get; set; }
    public decimal AssessedValue { get; set; }

    /// <summary>
    /// Taxable or exempt, from the exemptions approved and in force on the assessment's effective date, set when it is
    /// made (docs/analysis/assessment-listing-exemptions.md §4.1). An unproven claim leaves the line taxable (LGC §206).
    /// </summary>
    public Taxability Taxability { get; set; } = Taxability.Taxable;
    /// <summary>The exemption that decided the line: it made it exempt, or its ceiling left it taxable.</summary>
    public Guid? PropertyExemptionId { get; set; }
    public Exemptions.PropertyExemption? PropertyExemption { get; set; }
    /// <summary>Why the line is as it is when that is not plain from the exemption (a ceiling exceeded, an older exempt declaration).</summary>
    public string? TaxabilityNote { get; set; }
}
