using Prime.Domain.Common;

namespace Prime.Domain.Entities;

/// <summary>
/// The work file of one SMV revision cycle (LAM 2025 Book IV Chs. II–III; RA 12001 §§15–17; docs/analysis/
/// smv-preparation-general-revision.md §4.2): the date of valuation, the base valuation date sales are adjusted to, the
/// proposed SMV it produces, its consultations, and the dated events of its review and certification. Owned by the
/// Provincial Assessor's Office (Q2). The statutory periods are settings used only for due dates and reminders; nothing
/// changes by itself when one passes. The stage dates on <see cref="ProposedSmv"/> are written from the events.
/// </summary>
public sealed class SmvPreparation : AuditableEntity
{
    public int RevisionYear { get; set; }
    public string Title { get; set; } = string.Empty;
    /// <summary>The date of valuation (in January of the first year, Book IV p.112).</summary>
    public DateOnly? DateOfValuation { get; set; }
    /// <summary>The date sales are adjusted to: the planned date of submission to the BLGF, then the actual one.</summary>
    public DateOnly? BaseValuationDate { get; set; }
    public Guid ProposedSmvId { get; set; }
    public Smv? ProposedSmv { get; set; }
    public SmvPreparationStatus Status { get; set; } = SmvPreparationStatus.Preparing;
    public string? Notes { get; set; }
    public DateTimeOffset? CancelledAt { get; set; }
    public string? CancellationReason { get; set; }

    public List<SmvConsultation> Consultations { get; set; } = [];
    public List<SmvPreparationEvent> Events { get; set; } = [];
}

/// <summary>Where a preparation stands, from its latest event.</summary>
public enum SmvPreparationStatus
{
    Preparing = 0,
    PublishedForComment = 1,
    Submitted = 2,
    /// <summary>Endorsed by the BLGF Regional Office, under review by the BLGF.</summary>
    UnderReview = 3,
    Remanded = 4,
    Certified = 5,
    /// <summary>Not certified within the period: the existing SMV stays in force.</summary>
    NotCertified = 6,
    Published = 7,
    Cancelled = 8,
}

/// <summary>A public consultation on the proposed SMV (RA 12001 §15; Book IV p.105).</summary>
public sealed class SmvConsultation : AuditableEntity
{
    public Guid SmvPreparationId { get; set; }
    public DateOnly HeldOn { get; set; }
    public SmvConsultationMode Mode { get; set; }
    /// <summary>Venue or link.</summary>
    public string? Venue { get; set; }
    public int? Attendance { get; set; }
    public string? MinutesReference { get; set; }
    public string? Notes { get; set; }
}

public enum SmvConsultationMode
{
    InPerson = 0,
    Online = 1,
    Hybrid = 2,
}

/// <summary>A dated step of the proposed SMV's path to certification, as it happened.</summary>
public sealed class SmvPreparationEvent : AuditableEntity
{
    public Guid SmvPreparationId { get; set; }
    public SmvPreparationEventKind Kind { get; set; }
    public DateOnly OccurredOn { get; set; }
    /// <summary>The document: publication, transmittal, endorsement, remand letter, certification.</summary>
    public string? Reference { get; set; }
    /// <summary>The remand's reasons; otherwise a note.</summary>
    public string? Note { get; set; }
}

public enum SmvPreparationEventKind
{
    PublishedForComment = 0,
    SubmittedToRegionalOffice = 1,
    EndorsedByRegionalOffice = 2,
    EndorsedByBlgf = 3,
    Remanded = 4,
    Resubmitted = 5,
    Certified = 6,
    NotCertified = 7,
    Published = 8,
    TransmittedToSanggunian = 9,
}

/// <summary>
/// A factor adjusting sale prices of a period to the base valuation date for market trends (Book IV p.113; Q6). The LAM names
/// no method: the assessor enters the factor per month or quarter, with its source. DOMAIN VERIFICATION REQUIRED.
/// </summary>
public sealed class SmvTimeAdjustmentFactor : AuditableEntity
{
    public Guid SmvPreparationId { get; set; }
    public DateOnly PeriodFrom { get; set; }
    public DateOnly PeriodTo { get; set; }
    /// <summary>Multiplies the unit price: 1.05 raises it by 5%.</summary>
    public decimal Factor { get; set; }
    public string Source { get; set; } = string.Empty;
}

/// <summary>
/// The sales analysis of one class of land — and one crop, for agricultural land — behind the proposed unit values (SMV Forms
/// 2–4, 6–8; docs/analysis/smv-preparation-general-revision.md §4.2, Q7). The accepted sales are copied in when it is made or
/// refreshed; their adjusted prices are recomputed whenever a factor or a parameter changes. The ranges are computed on reading
/// (<c>SalesAnalysisMath</c>); the assessor's merges, sub-classes and adopted values are the groups.
/// </summary>
public sealed class SalesAnalysis : AuditableEntity
{
    public Guid SmvPreparationId { get; set; }
    public Guid ClassificationId { get; set; }
    public Reference.Classification? Classification { get; set; }
    /// <summary>The crop or use the analysis is limited to (rice land, coconut land …); null: every use of the class.</summary>
    public Guid? ActualUseId { get; set; }
    public Reference.ActualUse? ActualUse { get; set; }
    public DateOnly? SalesFrom { get; set; }
    public DateOnly? SalesTo { get; set; }
    /// <summary>The unit the unit values are per: square metres, or hectares for agricultural land.</summary>
    public Enums.AreaMeasure AreaUnit { get; set; }
    /// <summary>Unit values are rounded to this (the annexes: the nearest hundred).</summary>
    public decimal RoundingIncrement { get; set; } = 100m;
    /// <summary>The ranges' width, ±percent; null: the average interval.</summary>
    public decimal? RangeWidthPercent { get; set; }
    public string? Notes { get; set; }
    public List<SalesAnalysisScope> Scope { get; set; } = [];
    public List<SalesAnalysisSale> Sales { get; set; } = [];
    public List<SalesAnalysisGroup> Groups { get; set; } = [];
}

/// <summary>A city or municipality (market area) whose sales an analysis takes.</summary>
public sealed class SalesAnalysisScope : Entity
{
    public Guid SalesAnalysisId { get; set; }
    public Guid MunicipalityId { get; set; }
    public Reference.Municipality? Municipality { get; set; }
}

/// <summary>One sale in an analysis (Form 2 / 6 row): as recorded, then adjusted and rounded (Form 3 / 7).</summary>
public sealed class SalesAnalysisSale : Entity
{
    public Guid SalesAnalysisId { get; set; }
    public Guid MarketTransactionId { get; set; }
    public DateOnly TransactionDate { get; set; }
    public Guid MunicipalityId { get; set; }
    public Guid? BarangayId { get; set; }
    public Reference.Barangay? Barangay { get; set; }
    public string? Location { get; set; }
    public string? TaxDeclarationNumber { get; set; }
    public string? Pin { get; set; }
    public Guid? SubClassificationId { get; set; }
    public Reference.SubClassification? SubClassification { get; set; }
    /// <summary>In the analysis's unit.</summary>
    public decimal? Area { get; set; }
    /// <summary>The land's price: the consideration, or its land part when a building was conveyed with it.</summary>
    public decimal? Price { get; set; }
    public decimal? UnitPrice { get; set; }
    public decimal? TimeFactor { get; set; }
    /// <summary>The assessor's other adjustment, percent (+ or −): the road and distance deductions of agricultural land.</summary>
    public decimal OtherAdjustmentPercent { get; set; }
    public decimal? AdjustedUnitPrice { get; set; }
    public decimal? RoundedUnitValue { get; set; }
    /// <summary>The assessor left the sale out (with <see cref="ExclusionReason"/>).</summary>
    public bool LeftOut { get; set; }
    /// <summary>Why the sale is not analysed: the assessor's reason, or what PRIME could not compute.</summary>
    public string? ExclusionReason { get; set; }
    public string? Note { get; set; }
}

/// <summary>
/// One sub-class of an analysis (Table 3): the ranges it merges (by their rounded unit values), its sub-class, the value PRIME
/// proposes and the one the assessor adopts. Adopting writes a draft row in the proposed SMV; an adopted group is frozen.
/// </summary>
public sealed class SalesAnalysisGroup : AuditableEntity
{
    public Guid SalesAnalysisId { get; set; }
    /// <summary>Rounded unit values from (inclusive) …</summary>
    public decimal FromValue { get; set; }
    /// <summary>… to (inclusive).</summary>
    public decimal ToValue { get; set; }
    public Guid? SubClassificationId { get; set; }
    public Reference.SubClassification? SubClassification { get; set; }
    public decimal? AdoptedValue { get; set; }
    public string? Basis { get; set; }
    public Guid? SmvScheduleId { get; set; }
    public DateTimeOffset? AdoptedAt { get; set; }
    public Guid? AdoptedBy { get; set; }
}
