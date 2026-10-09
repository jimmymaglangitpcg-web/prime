using Prime.Domain.Common;
using Prime.Domain.Entities.Reference;
using Prime.Domain.Enums;

namespace Prime.Domain.Entities;

/// <summary>
/// A general revision of assessments over a scope of cities/municipalities on a certified SMV (LGC §219; RA 12001 §19;
/// LAM 2025 Book IV Ch. IV; docs/analysis/smv-preparation-general-revision.md §4.6). Every unit in scope becomes a
/// <see cref="GeneralRevisionItem"/> that records its previous and new values and why a run could not value it
/// (CLAUDE.md §33). Runs are <see cref="GeneralRevisionJob"/>s; the assessments they draft reference the run.
/// </summary>
public sealed class GeneralRevisionProgramme : AuditableEntity, IVersioned
{
    /// <summary>Row version (xmin): optimistic concurrency, see <see cref="IVersioned"/>.</summary>
    public uint RowVersion { get; set; }

    public int RevisionYear { get; set; }
    /// <summary>The revision's effectivity: every unit is valued and assessed as of it.</summary>
    public DateOnly EffectiveDate { get; set; }
    /// <summary>The certified SMV applied (GRI 2); approved in PRIME, in force on <see cref="EffectiveDate"/>.</summary>
    public Guid SmvId { get; set; }
    public Smv? Smv { get; set; }
    /// <summary>The local chief executive's office order (GRI 1).</summary>
    public string? OfficeOrderReference { get; set; }
    /// <summary>The ordinance providing for the revision (GRI 1).</summary>
    public string? OrdinanceReference { get; set; }
    public string? Description { get; set; }
    public GeneralRevisionStatus Status { get; set; } = GeneralRevisionStatus.Planned;
    public DateTimeOffset? CompletedAt { get; set; }
    public string? CancellationReason { get; set; }

    public List<GeneralRevisionScope> Scope { get; set; } = [];
    public List<GeneralRevisionSuspension> Suspensions { get; set; } = [];
}

/// <summary>A city/municipality a general revision covers.</summary>
public sealed class GeneralRevisionScope : Entity
{
    public Guid GeneralRevisionProgrammeId { get; set; }
    public Guid MunicipalityId { get; set; }
    public Municipality? Municipality { get; set; }
}

/// <summary>
/// A suspension of the revision (LAM 2025 Book IV p.125): during a local calamity for a set period, extendable on the
/// BLGF's recommendation, or a national emergency until lifted. No run starts while one is in force.
/// </summary>
public sealed class GeneralRevisionSuspension : AuditableEntity
{
    public Guid GeneralRevisionProgrammeId { get; set; }
    public GeneralRevisionSuspensionKind Kind { get; set; }
    public DateOnly FromDate { get; set; }
    /// <summary>The last day of the suspension; null until lifted (a national emergency).</summary>
    public DateOnly? UntilDate { get; set; }
    /// <summary>The declaration, or the Secretary's approval of an extension.</summary>
    public string Reference { get; set; } = string.Empty;
    public string? Remarks { get; set; }
    public DateOnly? LiftedOn { get; set; }
}

/// <summary>
/// One unit in a general revision: its place in tax-map order, the posted assessment it replaces, and the valuation and
/// Draft assessment a run made for it, or why the run could not (CLAUDE.md §33). Workflow progress after the run
/// (review, approval, posting) is read from the assessment itself.
/// </summary>
public sealed class GeneralRevisionItem : AuditableEntity
{
    public Guid GeneralRevisionProgrammeId { get; set; }
    public GeneralRevisionProgramme? GeneralRevisionProgramme { get; set; }
    public Guid RpuId { get; set; }
    public RealPropertyUnit? Rpu { get; set; }
    public Guid PropertyId { get; set; }
    public PropertyEntity? Property { get; set; }
    public Guid MunicipalityId { get; set; }
    public Guid BarangayId { get; set; }
    /// <summary>The PIN and unit number when compiled: the order the runs follow (GRI 15).</summary>
    public string Pin { get; set; } = string.Empty;
    public string RpuNumber { get; set; } = string.Empty;
    public RpuType RpuType { get; set; }

    public Guid? PreviousAssessmentId { get; set; }
    public Assessment? PreviousAssessment { get; set; }
    public decimal? PreviousMarketValue { get; set; }
    public decimal? PreviousAssessedValue { get; set; }

    public GeneralRevisionItemStatus Status { get; set; } = GeneralRevisionItemStatus.Pending;
    public string? FailureReason { get; set; }
    public Guid? ValuationId { get; set; }
    public Guid? AssessmentId { get; set; }
    public Assessment? Assessment { get; set; }
    public decimal? NewMarketValue { get; set; }
    public decimal? NewAssessedValue { get; set; }
    /// <summary>The run that last processed it.</summary>
    public Guid? LastRunId { get; set; }
    public DateTimeOffset? ProcessedAt { get; set; }

    // Field review (GRI 9–11): the ocular inspection reconciling class, use, owners and possessors. Changes found are
    // made through the ordinary screens; recording them puts the item back to Pending for the next value run.
    /// <summary>The appraiser assigned to inspect the unit.</summary>
    public Guid? InspectorId { get; set; }
    /// <summary>The route or assignment the inspection belongs to.</summary>
    public string? InspectionRoute { get; set; }
    public DateOnly? InspectedOn { get; set; }
    public Guid? InspectionRecordedBy { get; set; }
    public string? InspectionNotes { get; set; }
    /// <summary>The inspection found changes, so the unit had to be valued again.</summary>
    public bool? InspectionFoundChanges { get; set; }

    /// <summary>Why the unit was taken out of the revision (status Excluded).</summary>
    public string? ExclusionReason { get; set; }
}

/// <summary>
/// A step of the general revision instructions' checklist (GRI; LAM 2025 Book IV pp.121–125), as configuration: the LAM's text is
/// loaded as content and never committed (CLAUDE.md §118; Q13). A step naming a <see cref="Gate"/> is checked by PRIME.
/// </summary>
public sealed class GeneralRevisionChecklistStepDefinition : EffectiveDatedConfiguration
{
    public string Code { get; set; } = string.Empty;
    public int Sequence { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public GeneralRevisionGate? Gate { get; set; }
}

/// <summary>A programme's copy of a checklist step, taken when the checklist is loaded, with its completion.</summary>
public sealed class GeneralRevisionChecklistStep : AuditableEntity
{
    public Guid GeneralRevisionProgrammeId { get; set; }
    public Guid DefinitionId { get; set; }
    public string Code { get; set; } = string.Empty;
    public int Sequence { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public GeneralRevisionGate? Gate { get; set; }
    /// <summary>A step without a gate: when it was done, by whom, and the evidence (an office order number, a report reference).</summary>
    public DateOnly? CompletedOn { get; set; }
    public Guid? CompletedBy { get; set; }
    public string? Evidence { get; set; }
}

/// <summary>
/// An item a batch run (submit, approve, reject, post, Tax Declarations) could not process, and why — for instance an
/// approver who created the assessment (maker-checker, Q14); the item is then unchanged. Or, not <see cref="Failed"/>, a note
/// on an item it did process (a post that prepared no Tax Declaration).
/// </summary>
public sealed class GeneralRevisionRunIssue : Entity
{
    public Guid GeneralRevisionJobId { get; set; }
    public Guid GeneralRevisionItemId { get; set; }
    public string Pin { get; set; } = string.Empty;
    public string RpuNumber { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public bool Failed { get; set; } = true;
}
