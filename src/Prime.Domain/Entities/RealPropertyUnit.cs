using Prime.Domain.Common;
using Prime.Domain.Enums;

namespace Prime.Domain.Entities;

/// <summary>CLAUDE.md §22. A property may have multiple RPUs.</summary>
public sealed class RealPropertyUnit : AuditableEntity
{
    public Guid PropertyId { get; set; }
    public PropertyEntity? Property { get; set; }

    public string RpuNumber { get; set; } = string.Empty;
    public RpuType RpuType { get; set; }
    public RecordStatus Status { get; set; } = RecordStatus.Active;

    public DateOnly EffectivityDate { get; set; }
    public DateOnly? EndDate { get; set; }

    public Guid? PreviousRpuId { get; set; }
    public RealPropertyUnit? PreviousRpu { get; set; }

    public Guid? ApprovedBy { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
}
