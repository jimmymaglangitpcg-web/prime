using Prime.Domain.Common;
using Prime.Domain.Entities.Reference;
using Prime.Domain.Enums;

namespace Prime.Domain.Entities;

/// <summary>CLAUDE.md §26. Machinery valuation must be configurable.</summary>
public sealed class Machinery : AuditableEntity
{
    public Guid RpuId { get; set; }
    public RealPropertyUnit? Rpu { get; set; }
    public Guid PropertyId { get; set; }
    public PropertyEntity? Property { get; set; }

    public Guid MachineryTypeId { get; set; }
    public MachineryType? MachineryType { get; set; }

    public string? Description { get; set; }
    public string? Brand { get; set; }
    public string? Model { get; set; }
    public string? SerialNumber { get; set; }

    public decimal? Capacity { get; set; }
    public string? CapacityUnit { get; set; }

    public DateOnly? DateAcquired { get; set; }
    public decimal AcquisitionCost { get; set; }
    public decimal? InstallationCost { get; set; }
    public decimal? OtherCost { get; set; }

    /// <summary>
    /// LGC §224(a): a brand-new machine's fair market value is its acquisition
    /// cost; "in all other cases" it is valued from <see cref="ReplacementCost"/>.
    /// </summary>
    public bool IsBrandNew { get; set; }

    /// <summary>
    /// Current replacement or reproduction cost, as appraised (LGC §224(a)).
    /// Required to value machinery that is not brand-new; not used otherwise.
    /// </summary>
    public decimal? ReplacementCost { get; set; }

    public int? EconomicLifeYears { get; set; }
    public int? RemainingLifeYears { get; set; }

    public decimal? Depreciation { get; set; }
    public decimal? MarketValue { get; set; }
    public decimal? AssessedValue { get; set; }

    public RecordStatus Status { get; set; } = RecordStatus.Active;
}
