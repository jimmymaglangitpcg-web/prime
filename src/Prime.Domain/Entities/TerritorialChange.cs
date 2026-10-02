using Prime.Domain.Common;
using Prime.Domain.Entities.Reference;
using Prime.Domain.Enums;

namespace Prime.Domain.Entities;

/// <summary>
/// A territorial change (LAM Bk II p.40 §4.E–F; docs/analysis/identification-numbering.md §4.3): an LGU is created,
/// or territory is transferred to another LGU, by law or court order. Every property of the barangays it moves gets
/// a new PIN under the receiving barangay's index numbers; the old PINs are retired with the legal basis. Created as
/// a Draft, approved by a second user, then run as a resumable background job (CLAUDE.md §73).
/// </summary>
public sealed class TerritorialChangeJob : AuditableEntity
{
    public TerritorialChangeKind Kind { get; set; }
    /// <summary>The creating law or the court order.</summary>
    public string LegalBasis { get; set; } = string.Empty;
    public string? Remarks { get; set; }
    public DateOnly EffectiveDate { get; set; }
    public TerritorialChangePinMode PinMode { get; set; }
    public WorkflowStatus Status { get; set; } = WorkflowStatus.Draft;
    public Guid? ApprovedBy { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
    public JobExecutionStatus? RunStatus { get; set; }
    public int TotalCount { get; set; }
    public int ProcessedCount { get; set; }
    public int FailedCount { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public List<TerritorialChangeMapping> Mappings { get; set; } = [];
    public List<TerritorialChangeItem> Items { get; set; } = [];
}

/// <summary>A barangay whose properties move, and the barangay (with its index numbers) they move to.</summary>
public sealed class TerritorialChangeMapping : Entity
{
    public Guid TerritorialChangeJobId { get; set; }
    public Guid SourceBarangayId { get; set; }
    public Barangay? SourceBarangay { get; set; }
    public Guid TargetBarangayId { get; set; }
    public Barangay? TargetBarangay { get; set; }
}

/// <summary>One property of a territorial change: its old and new PIN, and how its processing went.</summary>
public sealed class TerritorialChangeItem : Entity
{
    public Guid TerritorialChangeJobId { get; set; }
    public Guid PropertyId { get; set; }
    public string OldPin { get; set; } = string.Empty;
    public string? NewPin { get; set; }
    public TerritorialChangeItemStatus Status { get; set; }
    public string? Error { get; set; }
    public DateTimeOffset? ProcessedAt { get; set; }
}

/// <summary>
/// The part of a property's parcel in one barangay, when a barangay line crosses it (LAM Bk II p.59): its area and
/// its share of the assessed value, printed as the annotation. The PIN uses the barangay of the larger part.
/// </summary>
public sealed class PropertyBarangayPart : Entity
{
    public Guid PropertyId { get; set; }
    public PropertyEntity? Property { get; set; }
    public int Sequence { get; set; }
    public Guid BarangayId { get; set; }
    public Barangay? Barangay { get; set; }
    public decimal Area { get; set; }
    /// <summary>Percent of the property's assessed value in this barangay; the parts total 100.</summary>
    public decimal AssessedValueShare { get; set; }
}
