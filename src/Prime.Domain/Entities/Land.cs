using Prime.Domain.Common;
using Prime.Domain.Entities.Reference;
using Prime.Domain.Enums;

namespace Prime.Domain.Entities;

/// <summary>
/// CLAUDE.md §24. <see cref="MarketValue"/>/<see cref="AssessedValue"/> are
/// computed by the Phase 5 valuation/assessment engine, never entered
/// directly — nullable here because no valuation exists yet in Phase 3.
/// </summary>
public sealed class Land : AuditableEntity
{
    public Guid RpuId { get; set; }
    public RealPropertyUnit? Rpu { get; set; }
    public Guid PropertyId { get; set; }
    public PropertyEntity? Property { get; set; }

    public decimal Area { get; set; }
    public string AreaUnit { get; set; } = "sqm";

    public Guid ClassificationId { get; set; }
    public Classification? Classification { get; set; }
    public Guid ActualUseId { get; set; }
    public ActualUse? ActualUse { get; set; }
    public Guid? SubClassificationId { get; set; }
    public SubClassification? SubClassification { get; set; }
    public Guid? ZoneId { get; set; }
    public Zone? Zone { get; set; }

    public decimal? LocationFactor { get; set; }
    public decimal? RoadFrontage { get; set; }
    public Guid? RoadTypeId { get; set; }
    public RoadType? RoadType { get; set; }
    public bool IsCornerLot { get; set; }
    public string? Zoning { get; set; }

    public decimal? MarketValue { get; set; }
    public decimal? AssessedValue { get; set; }

    public RecordStatus Status { get; set; } = RecordStatus.Active;

    /// <summary>
    /// The appraisal strips (MRPAAO Att. 1 "Land Appraisal"). <see cref="Area"/>
    /// is their total; <see cref="ClassificationId"/>, <see cref="ActualUseId"/>
    /// and <see cref="SubClassificationId"/> mirror the principal (largest) strip.
    /// </summary>
    public List<LandStrip> Strips { get; set; } = [];
    public List<LandImprovement> Improvements { get; set; } = [];
    public List<LandAdjustment> Adjustments { get; set; } = [];
}

/// <summary>A part of the land under one classification and actual use, valued at its own SMV rate.</summary>
public sealed class LandStrip : AuditableEntity
{
    public Guid LandId { get; set; }
    public int Sequence { get; set; }
    public Guid ClassificationId { get; set; }
    public Classification? Classification { get; set; }
    public Guid? SubClassificationId { get; set; }
    public SubClassification? SubClassification { get; set; }
    public Guid ActualUseId { get; set; }
    public ActualUse? ActualUse { get; set; }
    /// <summary>Null: the land's zone.</summary>
    public Guid? ZoneId { get; set; }
    public Zone? Zone { get; set; }
    /// <summary>In the land's area unit.</summary>
    public decimal Area { get; set; }
}

/// <summary>
/// Trees, plants and other non-building improvements on the land (MRPAAO
/// Att. 1 "Other Improvements"), valued at the SMV rate for their kind.
/// Assessed under their own classification and actual use, or the principal
/// strip's when none is given.
/// </summary>
public sealed class LandImprovement : AuditableEntity
{
    public Guid LandId { get; set; }
    public int Sequence { get; set; }
    public Guid ImprovementKindId { get; set; }
    public ImprovementKind? ImprovementKind { get; set; }
    public decimal Quantity { get; set; }
    /// <summary>Bearing / non-bearing where the SMV distinguishes them; null when it does not. DOMAIN VERIFICATION REQUIRED.</summary>
    public bool? IsProductive { get; set; }
    public Guid? ClassificationId { get; set; }
    public Classification? Classification { get; set; }
    public Guid? ActualUseId { get; set; }
    public ActualUse? ActualUse { get; set; }
    public string? Description { get; set; }
}

/// <summary>
/// A market value adjustment (MRPAAO Att. 1 "Market Value"): the factor is
/// named by code, and valued with the <see cref="AdjustmentFactor"/> of that
/// code in force under the SMV that priced the strip — so a general revision
/// under a new SMV applies the new ordinance's percentage.
/// </summary>
public sealed class LandAdjustment : AuditableEntity
{
    public Guid LandId { get; set; }
    /// <summary>Null: every strip.</summary>
    public Guid? LandStripId { get; set; }
    public string FactorCode { get; set; } = string.Empty;
    public string? Remarks { get; set; }
}
