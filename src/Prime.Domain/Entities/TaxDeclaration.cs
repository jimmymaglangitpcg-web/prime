using Prime.Domain.Common;
using Prime.Domain.Entities.Reference;
using Prime.Domain.Enums;

namespace Prime.Domain.Entities;

/// <summary>
/// CLAUDE.md §23. Never physically delete historical Tax Declarations —
/// superseded by a new row via <see cref="PreviousTaxDeclarationId"/>, not
/// overwritten. <see cref="Status"/> is the maker-checker
/// <see cref="WorkflowStatus"/> (CLAUDE.md §45), not an LGU-configurable
/// classification.
/// </summary>
public sealed class TaxDeclaration : AuditableEntity
{
    public Guid RpuId { get; set; }
    public RealPropertyUnit? Rpu { get; set; }
    public Guid PropertyId { get; set; }
    public PropertyEntity? Property { get; set; }

    public string TaxDeclarationNumber { get; set; } = string.Empty;
    public int RevisionNumber { get; set; } = 1;
    public DateOnly EffectivityDate { get; set; }
    public Taxability Taxability { get; set; } = Taxability.Taxable;

    public Guid ClassificationId { get; set; }
    public Classification? Classification { get; set; }
    public Guid ActualUseId { get; set; }
    public ActualUse? ActualUse { get; set; }
    public Guid? SubClassificationId { get; set; }
    public SubClassification? SubClassification { get; set; }

    public int AssessmentYear { get; set; }
    public WorkflowStatus Status { get; set; } = WorkflowStatus.Draft;

    public Guid? PreviousTaxDeclarationId { get; set; }
    public TaxDeclaration? PreviousTaxDeclaration { get; set; }

    public string? Remarks { get; set; }
    public Guid? ApprovedBy { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
}
