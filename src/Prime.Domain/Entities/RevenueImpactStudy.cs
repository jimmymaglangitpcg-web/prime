using Prime.Domain.Common;
using Prime.Domain.Entities.Reference;

namespace Prime.Domain.Entities;

/// <summary>
/// A revenue compliance and tax impact study of an SMV (RA 12001 §17; LAM 2025 Book IV pp.116–118; docs/analysis/
/// smv-preparation-general-revision.md §4.5): the units of a simulation run (L6-3), taxed under the current values, under the
/// new values at the existing levels and rates, and under up to three options. The rates and the year's collection are the
/// Treasurer's figures, entered here with their source; PRIME never reads them from the treasury records (CLAUDE.md §0) and never
/// computes a bill. Options are study data: nothing is approved by being in a study. Results are computed on reading from the
/// run's frozen results and the posted assessments.
/// </summary>
public sealed class RevenueImpactStudy : AuditableEntity, IVersioned
{
    /// <summary>Row version (xmin): optimistic concurrency, see <see cref="IVersioned"/>.</summary>
    public uint RowVersion { get; set; }

    public string Title { get; set; } = string.Empty;
    public Guid SmvSimulationRunId { get; set; }
    public SmvSimulationRun? SmvSimulationRun { get; set; }
    /// <summary>The year of the revenue compliance study.</summary>
    public int Year { get; set; }
    /// <summary>The date the taxable assessed values of the compliance study are read as of.</summary>
    public DateOnly ReferenceDate { get; set; }
    /// <summary>The year's collection of the current year's tax, without penalties and prior years (the Treasurer's figure).</summary>
    public decimal? ActualCollection { get; set; }
    /// <summary>Discounts given for advance and prompt payment that year (the Treasurer's figure).</summary>
    public decimal? Discounts { get; set; }
    public string? CollectionSource { get; set; }
    /// <summary>The LAM studies taxable land parcels; this extends the tax impact to every taxable unit (Q12).</summary>
    public bool IncludeAllTaxableUnits { get; set; }
    public string? Notes { get; set; }
    /// <summary>The existing rates (basic, special education fund …), as the Treasurer supplied them.</summary>
    public List<RevenueImpactRate> Rates { get; set; } = [];
    public List<RevenueImpactOption> Options { get; set; } = [];
}

/// <summary>An existing tax rate on a study.</summary>
public sealed class RevenueImpactRate : Entity
{
    public Guid RevenueImpactStudyId { get; set; }
    public string Label { get; set; } = string.Empty;
    public decimal RatePercent { get; set; }
    public string Source { get; set; } = string.Empty;
}

/// <summary>A tax option of a study (Book IV p.117 step 2): its total rate and the assessment levels it would use.</summary>
public sealed class RevenueImpactOption : Entity
{
    public Guid RevenueImpactStudyId { get; set; }
    public int Sequence { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal RatePercent { get; set; }
    public string? Description { get; set; }
    public List<RevenueImpactOptionLevel> Levels { get; set; } = [];
}

/// <summary>
/// An assessment level of an option, for a class (and a use, if given) and a bracket of market value ("over the lower, not over
/// the upper", as the levels PRIME applies). A row with no matching level keeps its level under the new values.
/// </summary>
public sealed class RevenueImpactOptionLevel : Entity
{
    public Guid RevenueImpactOptionId { get; set; }
    public Guid ClassificationId { get; set; }
    public Classification? Classification { get; set; }
    public Guid? ActualUseId { get; set; }
    public ActualUse? ActualUse { get; set; }
    public decimal LowerValue { get; set; }
    public decimal? UpperValue { get; set; }
    public decimal Percent { get; set; }
}
