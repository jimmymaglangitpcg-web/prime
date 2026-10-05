using Prime.Domain.Common;
using Prime.Domain.Entities.Reference;
using Prime.Domain.Enums;

namespace Prime.Domain.Entities;

/// <summary>
/// A simulation of the values a proposed SMV would give (docs/analysis/smv-preparation-general-revision.md §4.3): every
/// active unit with a current Tax Declaration in the scope, valued as of <see cref="AsOf"/> (the proposed effectivity)
/// under the SMV's rows and tables whatever their status, and assessed at the levels in force then. Nothing it computes
/// is a valuation or an assessment: results are kept apart and never posted. Runs as a background job (CLAUDE.md §73).
/// </summary>
public sealed class SmvSimulationRun : AuditableEntity
{
    public Guid SmvId { get; set; }
    public Smv? Smv { get; set; }
    public DateOnly AsOf { get; set; }
    public string? Description { get; set; }
    public List<SmvSimulationScope> Scope { get; set; } = [];

    public JobExecutionStatus Status { get; set; } = JobExecutionStatus.Queued;
    public int TotalCount { get; set; }
    public int ProcessedCount { get; set; }
    public int FailedCount { get; set; }
    public Guid? StartedBy { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public string? Remarks { get; set; }
}

/// <summary>A city or municipality a simulation covers.</summary>
public sealed class SmvSimulationScope : Entity
{
    public Guid SmvSimulationRunId { get; set; }
    public Guid MunicipalityId { get; set; }
    public Municipality? Municipality { get; set; }
}

/// <summary>
/// One unit's result in a simulation: its current posted values beside the simulated ones, or why it could not be
/// valued or assessed under the proposed SMV. The principal classification is the one of its largest assessment row.
/// </summary>
public sealed class SmvSimulationResult : Entity
{
    public Guid SmvSimulationRunId { get; set; }
    public Guid RpuId { get; set; }
    public Guid PropertyId { get; set; }
    public Guid MunicipalityId { get; set; }
    public Guid BarangayId { get; set; }
    public string Pin { get; set; } = string.Empty;
    public string RpuNumber { get; set; } = string.Empty;
    public RpuType RpuType { get; set; }

    public Guid? CurrentAssessmentId { get; set; }
    public Guid? CurrentClassificationId { get; set; }
    public Classification? CurrentClassification { get; set; }
    public decimal? CurrentMarketValue { get; set; }
    public decimal? CurrentAssessedValue { get; set; }

    public Guid? SimulatedClassificationId { get; set; }
    public Classification? SimulatedClassification { get; set; }
    public decimal? SimulatedMarketValue { get; set; }
    public decimal? SimulatedAssessedValue { get; set; }
    /// <summary>The simulated assessed value of the rows that would be taxable.</summary>
    public decimal? SimulatedTaxableAssessedValue { get; set; }

    /// <summary>Why the unit could not be valued (no market value) or assessed (a market value, no assessed value).</summary>
    public string? FailureReason { get; set; }
    public DateTimeOffset ProcessedAt { get; set; }
    /// <summary>The simulated assessment rows, so a tax option can apply its own levels to them (L6-5).</summary>
    public List<SmvSimulationResultLine> Lines { get; set; } = [];
}

/// <summary>One simulated assessment row of a unit: its class and use, market and assessed value, and whether it would be taxable.</summary>
public sealed class SmvSimulationResultLine : Entity
{
    public Guid SmvSimulationResultId { get; set; }
    public int Sequence { get; set; }
    public Guid ClassificationId { get; set; }
    public Guid ActualUseId { get; set; }
    public decimal MarketValue { get; set; }
    public decimal AssessedValue { get; set; }
    public bool Taxable { get; set; }
}

/// <summary>
/// A valuation test of an SMV (LAM 2025 Book IV pp.115–116; docs/analysis/smv-preparation-general-revision.md §4.3, Q9):
/// each accepted land sale in the scope and period, valued at its area times the SMV's unit value for its class,
/// sub-class and use (no lot adjustments), against its land price. The rows are frozen when the test is made, so a
/// later change to the SMV or the sales does not change it. Prices are the recorded ones until sales can be adjusted
/// to a base valuation date (L6-2).
/// </summary>
public sealed class ValuationTestRun : AuditableEntity
{
    public Guid SmvId { get; set; }
    public Smv? Smv { get; set; }
    /// <summary>The date the SMV's rows are read as of (the proposed effectivity).</summary>
    public DateOnly AsOf { get; set; }
    /// <summary>The period of the sales tested; null: from the first, or to the last.</summary>
    public DateOnly? SalesFrom { get; set; }
    public DateOnly? SalesTo { get; set; }
    public string? Description { get; set; }
    public List<ValuationTestScope> Scope { get; set; } = [];
    public List<ValuationTestSale> Sales { get; set; } = [];
}

/// <summary>A city or municipality whose sales a valuation test takes.</summary>
public sealed class ValuationTestScope : Entity
{
    public Guid ValuationTestRunId { get; set; }
    public Guid MunicipalityId { get; set; }
    public Municipality? Municipality { get; set; }
}

/// <summary>One sale in a valuation test: its price, the unit value and value under the SMV, the ratio — or why it was left out.</summary>
public sealed class ValuationTestSale : Entity
{
    public Guid ValuationTestRunId { get; set; }
    public Guid MarketTransactionId { get; set; }
    public DateOnly TransactionDate { get; set; }
    public Guid MunicipalityId { get; set; }
    public Guid? BarangayId { get; set; }
    public Barangay? Barangay { get; set; }
    public Guid? ClassificationId { get; set; }
    public Classification? Classification { get; set; }
    public Guid? SubClassificationId { get; set; }
    public SubClassification? SubClassification { get; set; }
    public Guid? ActualUseId { get; set; }
    public decimal? LandArea { get; set; }
    public AreaMeasure LandAreaUnit { get; set; }
    /// <summary>The land's price: the consideration, or its land part when a building was conveyed with it.</summary>
    public decimal? Price { get; set; }
    public Guid? SmvScheduleId { get; set; }
    public string? RateUnit { get; set; }
    public decimal? UnitValue { get; set; }
    public decimal? Value { get; set; }
    public decimal? Ratio { get; set; }
    public string? ExclusionReason { get; set; }
}
