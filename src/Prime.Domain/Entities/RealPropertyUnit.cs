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

    /// <summary>
    /// The unit's postscript to the property's PIN — buildings 1001, 1002 …,
    /// machinery 2001 … (MRPAAO p.42; docs/analysis/mrpaao-forms-model.md §6.2).
    /// Assigned once on creation, never reused within the property. Null for
    /// land and for types without a configured series.
    /// </summary>
    public int? PinSuffix { get; set; }

    /// <summary>For a building, machinery or other improvement: the land unit it stands on (same property).</summary>
    public Guid? LandRpuId { get; set; }
    public RealPropertyUnit? LandRpu { get; set; }

    /// <summary>
    /// For machinery: the building unit it is installed in (same property); for a condominium or leasing unit: the
    /// leasing property it is part of.
    /// </summary>
    public Guid? HostRpuId { get; set; }
    public RealPropertyUnit? HostRpu { get; set; }

    // The LAM's structured unit PIN (Book II pp.38–39; docs/analysis/identification-numbering.md §4.2).
    /// <summary>A building that is a leasing property (condominium): its <see cref="PinSuffix"/> is from the leasing series (3001 …), printed in parentheses.</summary>
    public bool IsLeasingProperty { get; set; }
    /// <summary>A unit of a leasing property: its floor's prefix (F, or B, P, M for basement, parking, mezzanine) and number.</summary>
    public string? FloorPrefix { get; set; }
    public int? FloorNumber { get; set; }
    /// <summary>A unit of a leasing property: its number on the floor.</summary>
    public int? UnitNumber { get; set; }

    public Guid? ApprovedBy { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
}
