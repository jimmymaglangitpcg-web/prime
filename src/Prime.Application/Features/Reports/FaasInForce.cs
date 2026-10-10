namespace Prime.Application.Features.Reports;

/// <summary>One FAAS in force on a date, with its values split into taxable and exempt parts (see <see cref="IFaasInForceQuery"/>).</summary>
public sealed class FaasValue
{
    public Guid TaxDeclarationId { get; init; }
    public string TaxDeclarationNumber { get; init; } = string.Empty;
    public Guid PropertyId { get; init; }
    public Guid RpuId { get; init; }
    public bool IsLand { get; init; }
    /// <summary>The unit's kind, as stored (<see cref="Domain.Enums.RpuType"/>'s name).</summary>
    public string RpuType { get; init; } = string.Empty;
    public Guid MunicipalityId { get; init; }
    public Guid BarangayId { get; init; }
    public Guid? ZoneId { get; init; }
    public Guid ClassificationId { get; init; }
    public Guid ActualUseId { get; init; }
    public decimal TaxableMarketValue { get; init; }
    public decimal TaxableAssessedValue { get; init; }
    public decimal ExemptMarketValue { get; init; }
    public decimal ExemptAssessedValue { get; init; }
    /// <summary>A land unit's area in square metres; null for other kinds and for land in a unit not converted.</summary>
    public decimal? LandAreaSqm { get; init; }
    /// <summary>A land unit whose area unit is neither square metres nor hectares: its area is left out of the totals.</summary>
    public bool LandAreaUnconverted { get; init; }
}

/// <summary>Which FAAS a query covers: those in force on <see cref="AsOf"/>, of the named place or properties, in the request's jurisdiction.</summary>
public sealed record FaasScope(DateOnly AsOf, Guid? MunicipalityId = null, Guid? BarangayId = null, IReadOnlyCollection<Guid>? PropertyIds = null);

/// <summary>What <see cref="IFaasInForceQuery.SummaryAsync"/> groups by; <see cref="None"/> gives the total only.</summary>
public enum FaasGroupBy
{
    None,
    Barangay,
    Classification,
    ActualUse,
    Zone,
}

/// <summary>A group's sums; the row with <see cref="IsTotal"/> is the whole scope's, counting each property once.</summary>
public sealed class FaasGroup
{
    /// <summary>What the row is grouped by; <see cref="FaasGroupBy.None"/> on the total.</summary>
    public FaasGroupBy GroupBy { get; init; }
    public Guid? Key { get; init; }
    /// <summary>
    /// The unit kind (<see cref="Domain.Enums.RpuType"/>'s name) of a <see cref="IFaasInForceQuery.KindSummaryAsync"/> row; null on
    /// the total and on the other summaries.
    /// </summary>
    public string? Kind { get; init; }
    public bool IsTotal { get; init; }
    public int Properties { get; init; }
    public int Units { get; init; }
    public decimal LandAreaSqm { get; init; }
    public int UnconvertedLandUnits { get; init; }
    public decimal TaxableMarketValue { get; init; }
    public decimal TaxableAssessedValue { get; init; }
    public decimal ExemptMarketValue { get; init; }
    public decimal ExemptAssessedValue { get; init; }
}

/// <summary>
/// The FAAS in force on a date, read in the database (docs/analysis/reporting.md §4.1, Q9). It is the registers' rule
/// (<see cref="Registers.RegisterFormDataProvider"/>) written for the database, so a province is summed without reading its
/// records into memory (CLAUDE.md §71):
/// <list type="bullet">
/// <item>a TD effective by the date, approved by then (or approved without a recorded time), and approved still or
/// cancelled only after the date;</item>
/// <item>per unit, the latest such TD by effectivity, then revision, then creation;</item>
/// <item>its values: those of the assessment it declares, else the unit's posted assessment in force on the date (the
/// latest effective by then, the latest made among equals);</item>
/// <item>split by the assessment's lines when the TD declares an assessment that has lines, else wholly taxable or exempt
/// as the TD says (assessment-listing-exemptions.md §4.1, Q4).</item>
/// </list>
/// Both reads apply the request's jurisdiction themselves, as the screens' query filters do. A change to the registers'
/// rule must be made here too; the integration tests compare the two.
/// </summary>
public interface IFaasInForceQuery
{
    /// <summary>One row per FAAS; meant for a narrow scope (a page of properties), not a province.</summary>
    Task<IReadOnlyList<FaasValue>> ListAsync(FaasScope scope, CancellationToken cancellationToken);

    /// <summary>The sums per group and in total, in one pass over the scope.</summary>
    Task<IReadOnlyList<FaasGroup>> SummaryAsync(FaasScope scope, FaasGroupBy groupBy, CancellationToken cancellationToken) =>
        SummaryAsync(scope, groupBy == FaasGroupBy.None ? [] : [groupBy], cancellationToken);

    /// <summary>
    /// The sums per group of each grouping and in total, in one pass over the scope (the dashboard's figures, reporting.md
    /// §4.3): each row says which grouping it belongs to.
    /// </summary>
    Task<IReadOnlyList<FaasGroup>> SummaryAsync(FaasScope scope, IReadOnlyCollection<FaasGroupBy> groupings, CancellationToken cancellationToken);

    /// <summary>
    /// The sums per unit kind and classification (<see cref="FaasGroup.Kind"/> and <see cref="FaasGroup.Key"/>), per kind
    /// (<see cref="FaasGroup.Key"/> null) and in total, in one pass (the value summary, reporting.md §4.2).
    /// </summary>
    Task<IReadOnlyList<FaasGroup>> KindSummaryAsync(FaasScope scope, CancellationToken cancellationToken);
}
