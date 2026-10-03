using Prime.Domain.Common;
using Prime.Domain.Entities.Reference;
using Prime.Domain.Enums;

namespace Prime.Domain.Entities.MarketData;

/// <summary>
/// A real property transaction kept as market evidence for the SMV (LAM 2025 Book I p.22, Annex I-M; Book IV pp.104,
/// 113; docs/analysis/smv-preparation-general-revision.md §4.1). Recorded as stated by its source; the assessor reviews
/// it before it may enter a sales analysis. Never deleted: cancelled with a reason. Prices and parties are personal
/// data (CLAUDE.md §68).
/// </summary>
public sealed class MarketTransaction : AuditableEntity
{
    public MarketDataSource Source { get; set; }
    /// <summary>Mode of conveyance (sale, donation, partition, lease, mortgage …): configured lookup.</summary>
    public Guid? ConveyanceModeId { get; set; }
    public ConveyanceMode? ConveyanceMode { get; set; }
    public DateOnly TransactionDate { get; set; }
    public string? DocumentReference { get; set; }
    public string? DocumentFileNumber { get; set; }

    public string? GrantorNames { get; set; }
    public string? GranteeNames { get; set; }
    public string? GranteeAddress { get; set; }

    public Guid MunicipalityId { get; set; }
    public Municipality? Municipality { get; set; }
    public Guid? BarangayId { get; set; }
    public Barangay? Barangay { get; set; }
    /// <summary>Street, purok or sitio.</summary>
    public string? Location { get; set; }
    /// <summary>The PRIME property, when identified.</summary>
    public Guid? PropertyId { get; set; }
    public PropertyEntity? Property { get; set; }
    /// <summary>As stated by the source.</summary>
    public string? Pin { get; set; }
    public string? TaxDeclarationNumber { get; set; }
    public string? LotNumber { get; set; }
    public string? PreviousTitleNumber { get; set; }
    public string? NewTitleNumber { get; set; }

    public bool ConveysLand { get; set; } = true;
    public bool ConveysBuilding { get; set; }
    public Guid? ClassificationId { get; set; }
    public Classification? Classification { get; set; }
    public Guid? SubClassificationId { get; set; }
    public SubClassification? SubClassification { get; set; }
    /// <summary>Actual use, or the crop of agricultural land (rice land, coconut land …).</summary>
    public Guid? ActualUseId { get; set; }
    public ActualUse? ActualUse { get; set; }
    public Guid? BuildingTypeId { get; set; }
    public BuildingType? BuildingType { get; set; }
    public Guid? StructuralTypeId { get; set; }
    public StructuralType? StructuralType { get; set; }

    public decimal? LandArea { get; set; }
    public AreaMeasure LandAreaUnit { get; set; } = AreaMeasure.SquareMetre;
    public decimal? BuildingFloorArea { get; set; }

    /// <summary>The whole consideration stated.</summary>
    public decimal Consideration { get; set; }
    /// <summary>The part of the consideration for the land, when a building was conveyed with it.</summary>
    public decimal? LandConsideration { get; set; }
    /// <summary>Land price per <see cref="LandAreaUnit"/>; null when it cannot be told apart from a building's.</summary>
    public decimal? LandUnitPrice { get; set; }
    /// <summary>Building price per square metre of floor area, when it can be told apart.</summary>
    public decimal? BuildingUnitPrice { get; set; }

    public MarketDataReview Review { get; set; } = MarketDataReview.Unreviewed;
    public string? ExclusionReason { get; set; }
    public DateOnly? FieldValidatedOn { get; set; }
    public string? ReviewNote { get; set; }
    public Guid? ReviewedBy { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }

    /// <summary>The transfer it was prefilled from.</summary>
    public Guid? PropertyTransactionId { get; set; }
    /// <summary>The import it came in with.</summary>
    public string? ImportBatch { get; set; }
    public string? Remarks { get; set; }

    public DateTimeOffset? CancelledAt { get; set; }
    public Guid? CancelledBy { get; set; }
    public string? CancellationReason { get; set; }
}

/// <summary>
/// A building permit transmitted to the assessor (LGC §290; LAM 2025 Book I pp.22–23, Annex I-N), recorded as stated.
/// Linked to the building once it is declared; until then it is a discovery lead.
/// </summary>
public sealed class BuildingPermitAbstract : AuditableEntity
{
    public string PermitNumber { get; set; } = string.Empty;
    public DateOnly IssuedOn { get; set; }
    public DateOnly? ProposedConstructionDate { get; set; }
    public DateOnly? ExpectedCompletionDate { get; set; }
    public string PermitteeName { get; set; } = string.Empty;
    public string? PermitteeAddress { get; set; }
    /// <summary>As stated on the permit.</summary>
    public string? TaxDeclarationNumber { get; set; }

    public Guid MunicipalityId { get; set; }
    public Municipality? Municipality { get; set; }
    public Guid? BarangayId { get; set; }
    public Barangay? Barangay { get; set; }
    public string? BlockLotNumber { get; set; }
    public string? Street { get; set; }

    public BuildingPermitScope Scope { get; set; }
    public Guid? BuildingTypeId { get; set; }
    public BuildingType? BuildingType { get; set; }
    public Guid? StructuralTypeId { get; set; }
    public StructuralType? StructuralType { get; set; }
    public int? Storeys { get; set; }
    public decimal? TotalFloorArea { get; set; }
    public decimal? EstimatedCost { get; set; }
    public Guid? ClassificationId { get; set; }
    public Classification? Classification { get; set; }
    public DateOnly? ReceivedOn { get; set; }

    public Guid? BuildingId { get; set; }
    public Building? Building { get; set; }
    public string? Remarks { get; set; }

    public DateTimeOffset? CancelledAt { get; set; }
    public Guid? CancelledBy { get; set; }
    public string? CancellationReason { get; set; }
}

/// <summary>
/// A certificate of registration of installation of machinery transmitted to the assessor (LGC §210; LAM 2025 Book I
/// p.23, Annex I-O), recorded as stated. Linked to the machinery once declared; until then a discovery lead.
/// </summary>
public sealed class MachineryRegistrationAbstract : AuditableEntity
{
    public string CertificateNumber { get; set; } = string.Empty;
    public DateOnly IssuedOn { get; set; }
    public string OwnerName { get; set; } = string.Empty;
    public string? OwnerAddress { get; set; }
    public string? TaxDeclarationNumber { get; set; }

    public Guid MunicipalityId { get; set; }
    public Municipality? Municipality { get; set; }
    public Guid? BarangayId { get; set; }
    public Barangay? Barangay { get; set; }
    public string? Location { get; set; }

    public Guid? MachineryTypeId { get; set; }
    public MachineryType? MachineryType { get; set; }
    public string? Description { get; set; }
    public string? BrandModel { get; set; }
    public int? YearAcquired { get; set; }
    public string? Manufacturer { get; set; }
    public decimal? Cost { get; set; }
    public string? CurrentCondition { get; set; }
    public DateOnly? InstallationDate { get; set; }
    public DateOnly? ReceivedOn { get; set; }

    public Guid? MachineryId { get; set; }
    public Machinery? Machinery { get; set; }
    public string? Remarks { get; set; }

    public DateTimeOffset? CancelledAt { get; set; }
    public Guid? CancelledBy { get; set; }
    public string? CancellationReason { get; set; }
}

/// <summary>
/// One printing of an abstract or of the sales report for a municipality and a period, issued through the forms
/// foundation (frozen snapshot).
/// </summary>
public sealed class MarketDataReportRun : AuditableEntity
{
    public MarketDataReportKind Kind { get; set; }
    public Guid MunicipalityId { get; set; }
    public Municipality? Municipality { get; set; }
    public DateOnly FromDate { get; set; }
    public DateOnly ToDate { get; set; }
    public string? Remarks { get; set; }
}
