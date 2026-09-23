using Prime.Domain.Common;
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
public sealed class Assessment : AuditableEntity
{
    public Guid RpuId { get; set; }
    public RealPropertyUnit? Rpu { get; set; }
    public Guid PropertyId { get; set; }
    public PropertyEntity? Property { get; set; }

    public Guid ValuationId { get; set; }
    public Valuation? Valuation { get; set; }

    public int AssessmentYear { get; set; }
    public decimal MarketValue { get; set; }

    public Guid AssessmentLevelId { get; set; }
    public AssessmentLevel? AssessmentLevel { get; set; }
    public decimal AssessmentPercentage { get; set; }
    public decimal AssessedValue { get; set; }

    public WorkflowStatus Status { get; set; } = WorkflowStatus.Draft;
    public DateOnly EffectiveDate { get; set; }

    public Guid? PreviousAssessmentId { get; set; }
    public Assessment? PreviousAssessment { get; set; }

    /// <summary>
    /// Set when produced by a General Revision batch (a plain scalar Guid
    /// here, not yet an FK — <c>GeneralRevisionJob</c> doesn't exist until
    /// Phase 6 checkpoint C adds it; the relationship is wired up then).
    /// </summary>
    public Guid? RevisionReference { get; set; }

    public string? Remarks { get; set; }
    public Guid? ApprovedBy { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
}
