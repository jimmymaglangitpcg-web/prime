using Prime.Domain.Common;
using Prime.Domain.Entities.Reference;
using Prime.Domain.Enums;

namespace Prime.Domain.Entities;

/// <summary>CLAUDE.md §26. Machinery valuation must be configurable.</summary>
public sealed class Machinery : AuditableEntity, IVersioned
{
    /// <summary>Row version (xmin): optimistic concurrency, see <see cref="IVersioned"/>.</summary>
    public uint RowVersion { get; set; }

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

    // MRPAAO Att. 3 (docs/analysis/mrpaao-forms-model.md §10).
    public int? YearInstalled { get; set; }
    public int? YearOfInitialOperation { get; set; }
    /// <summary>
    /// Recorded and printed only: the replacement cost stays an entered figure
    /// (deriving it from this factor is DOMAIN VERIFICATION REQUIRED).
    /// </summary>
    public decimal? ConversionFactor { get; set; }

    public decimal? Depreciation { get; set; }
    public decimal? MarketValue { get; set; }
    public decimal? AssessedValue { get; set; }

    public RecordStatus Status { get; set; } = RecordStatus.Active;

    // Derived replacement cost (LAM Bk III pp.73–75; docs/analysis/valuation-foundation.md §4.6).
    /// <summary>Imported machinery: its cost is converted at the exchange rates of acquisition and valuation.</summary>
    public bool IsImported { get; set; }
    /// <summary>ISO 4217 code of the currency it was bought in (imported machinery).</summary>
    public string? AcquisitionCurrency { get; set; }
    /// <summary>Its cost in that currency; recorded for the FAAS (the peso <see cref="AcquisitionCost"/> is the basis).</summary>
    public decimal? ForeignAcquisitionCost { get; set; }
    public string? OriginCountry { get; set; }
    /// <summary>The price index series (an origin country's, or the local one) that trends its cost; null: the replacement cost is entered.</summary>
    public string? PriceIndexSeries { get; set; }
    /// <summary>Years of use count from here, else from <see cref="DateAcquired"/>.</summary>
    public DateOnly? DateInstalled { get; set; }
    /// <summary>LGC §225: the minimum remaining value holds only while the machine is useful and in operation (Q12).</summary>
    public bool IsInOperation { get; set; } = true;

    // Acquisition documents the LAM machinery FAAS prints (Annex I-F pp.157–163; records-and-forms.md §4.1, Q14).
    public string? EngineeringRegistrationNumber { get; set; }
    public DateOnly? EngineeringRegistrationDate { get; set; }
    public string? ImportPermitNumber { get; set; }
    public DateOnly? ImportPermitDate { get; set; }
    public string? SupplierName { get; set; }
    public string? SupplierAddress { get; set; }
    /// <summary>The official receipt of the purchase.</summary>
    public string? ReceiptNumber { get; set; }
    public DateOnly? ReceiptDate { get; set; }
    /// <summary>The acquisition cost items: freight, insurance, duties, installation … (LAM Bk III p.75).</summary>
    public List<MachineryCostItem> CostItems { get; set; } = [];

    /// <summary>
    /// The machine's own classification and actual use, when it differs from
    /// its unit's Tax Declaration's (MRPAAO Att. 3: one assessment row per
    /// use). Null: the TD's.
    /// </summary>
    public Guid? ClassificationId { get; set; }
    public Classification? Classification { get; set; }
    public Guid? ActualUseId { get; set; }
    public ActualUse? ActualUse { get; set; }
}
