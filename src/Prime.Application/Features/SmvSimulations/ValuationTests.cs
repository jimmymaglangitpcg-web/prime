using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Prime.Application.Common;
using Prime.Application.Common.Interfaces;
using Prime.Application.Features.Forms;
using Prime.Application.Features.Valuation;
using Prime.Domain.DomainServices;
using Prime.Domain.Entities;
using Prime.Domain.Enums;

namespace Prime.Application.Features.SmvSimulations;

/// <summary>
/// Benchmarks for valuation testing (LAM 2025 Book IV pp.115–116; Q10). The LAM's figures are indicative and there is no
/// international standard, so PRIME ships none: each is the province's, loaded with its configuration. Results are shown
/// either way; a benchmark only adds a mark.
/// </summary>
public sealed class ValuationTestingOptions
{
    public const string SectionName = "ValuationTesting";

    public decimal? MedianRatioLow { get; set; }
    public decimal? MedianRatioHigh { get; set; }
    /// <summary>The highest acceptable coefficient of dispersion, in percent.</summary>
    public decimal? MaximumCoefficientOfDispersion { get; set; }
}

public sealed record CreateValuationTestRequest(Guid SmvId, DateOnly AsOf, IReadOnlyList<Guid>? MunicipalityIds, DateOnly? SalesFrom, DateOnly? SalesTo,
    string? Description);

/// <summary>One group's statistics; the benchmark marks are null when no benchmark is configured.</summary>
public sealed record ValuationTestGroupDto(string Level, string Name, int Count, decimal? MedianRatio, decimal? CoefficientOfDispersion,
    bool? MedianWithinBenchmark, bool? DispersionWithinBenchmark);

public sealed record ValuationTestDto(
    Guid Id, Guid SmvId, string SmvReference, int SmvRevisionYear, DateOnly AsOf, DateOnly? SalesFrom, DateOnly? SalesTo, string? Description,
    IReadOnlyList<SmvSimulationMunicipalityDto> Municipalities, int SalesCount, int TestedCount, DateTimeOffset CreatedAt,
    IReadOnlyList<ValuationTestGroupDto>? Groups = null, ValuationTestingOptions? Benchmarks = null);

public sealed record ValuationTestSaleDto(
    Guid Id, Guid MarketTransactionId, DateOnly TransactionDate, string? MunicipalityName, string? BarangayName, string? ClassificationName,
    string? SubClassificationName, decimal? LandArea, AreaMeasure LandAreaUnit, decimal? Price, string? RateUnit, decimal? UnitValue, decimal? Value,
    decimal? Ratio, string? ExclusionReason);

public interface IValuationTestService
{
    /// <summary>Tests the SMV against the accepted land sales of the scope and period, and freezes the result.</summary>
    Task<Result<ValuationTestDto>> CreateAsync(CreateValuationTestRequest request, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<ValuationTestDto>>> ListAsync(Guid? smvId, CancellationToken cancellationToken = default);
    Task<Result<ValuationTestDto>> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<ValuationTestSaleDto>>> ListSalesAsync(Guid id, CancellationToken cancellationToken = default);
}

/// <summary>
/// Valuation testing (docs/analysis/smv-preparation-general-revision.md §4.3, Q9–Q10): for each accepted land sale, the
/// value is its land area times the SMV's unit value for its class, sub-class and use, read through the valuation
/// engine's rate selection (no lot adjustments); the ratio is value over the land price. Median ratio and coefficient of
/// dispersion per sub-class, class, city/municipality and overall. Sales are few enough to test in the request.
/// </summary>
public sealed class ValuationTestService(
    IApplicationDbContext db, IJurisdiction jurisdiction, IValuationService valuation, IOptions<ValuationTestingOptions> options) : IValuationTestService
{
    public async Task<Result<ValuationTestDto>> CreateAsync(CreateValuationTestRequest r, CancellationToken cancellationToken = default)
    {
        var ct = cancellationToken;
        var municipalities = (r.MunicipalityIds ?? []).Distinct().ToList();
        if (r.AsOf == default || municipalities.Count == 0 || r.SalesFrom > r.SalesTo || r.Description?.Length > 1000)
        {
            return Fail<ValuationTestDto>("VALIDATION_FAILED",
                "The SMV, the as-of date and at least one city/municipality are required; the sales period must not end before it starts; description max 1000.");
        }
        if (await db.Municipalities.CountAsync(x => municipalities.Contains(x.Id), ct) != municipalities.Count)
        {
            return Fail<ValuationTestDto>("MUNICIPALITY_NOT_FOUND", "A city/municipality in the scope does not exist.");
        }
        if (municipalities.Any(m => !jurisdiction.Allows(m)))
        {
            return Fail<ValuationTestDto>(JurisdictionErrors.Code, JurisdictionErrors.Message);
        }
        var smv = await db.Smvs.AsNoTracking().FirstOrDefaultAsync(x => x.Id == r.SmvId, ct);
        if (smv is null || smv.Status is WorkflowStatus.Rejected or WorkflowStatus.Cancelled or WorkflowStatus.Voided)
        {
            return Fail<ValuationTestDto>("SMV_NOT_FOUND", "The SMV does not exist, or was rejected or cancelled.");
        }

        var sales = await db.MarketTransactions.AsNoTracking()
            .Where(x => x.CancelledAt == null && x.Review == MarketDataReview.Accepted && x.ConveysLand && municipalities.Contains(x.MunicipalityId)
                && (r.SalesFrom == null || x.TransactionDate >= r.SalesFrom) && (r.SalesTo == null || x.TransactionDate <= r.SalesTo))
            .OrderBy(x => x.TransactionDate).ThenBy(x => x.CreatedAt)
            .ToListAsync(ct);
        var mode = ValuationMode.Proposed(smv.Id);
        var run = new ValuationTestRun
        {
            SmvId = smv.Id, AsOf = r.AsOf, SalesFrom = r.SalesFrom, SalesTo = r.SalesTo,
            Description = string.IsNullOrWhiteSpace(r.Description) ? null : r.Description.Trim(),
            Scope = municipalities.Select(m => new ValuationTestScope { MunicipalityId = m }).ToList(),
        };
        foreach (var sale in sales)
        {
            var row = new ValuationTestSale
            {
                MarketTransactionId = sale.Id, TransactionDate = sale.TransactionDate, MunicipalityId = sale.MunicipalityId, BarangayId = sale.BarangayId,
                ClassificationId = sale.ClassificationId, SubClassificationId = sale.SubClassificationId, ActualUseId = sale.ActualUseId,
                LandArea = sale.LandArea, LandAreaUnit = sale.LandAreaUnit,
                Price = sale.ConveysBuilding ? sale.LandConsideration : sale.Consideration,
            };
            run.Sales.Add(row);
            if (row.ClassificationId is not { } classificationId)
            {
                row.ExclusionReason = "The sale records no classification.";
                continue;
            }
            if (row.LandArea is not > 0)
            {
                row.ExclusionReason = "The sale records no land area.";
                continue;
            }
            if (row.Price is not > 0)
            {
                row.ExclusionReason = "The land's price cannot be told apart from the building's.";
                continue;
            }
            var rate = await valuation.LandRateAsync(classificationId, row.SubClassificationId, row.ActualUseId, row.MunicipalityId, row.BarangayId,
                r.AsOf, mode, ct);
            if (rate is null)
            {
                row.ExclusionReason = "The SMV gives no unit value for the sale's class, sub-class and use.";
                continue;
            }
            (row.SmvScheduleId, row.RateUnit, row.UnitValue) = (rate.SmvScheduleId, rate.Unit, rate.UnitValue);
            if (AreaIn(rate.Unit, row.LandArea.Value, row.LandAreaUnit) is not { } area)
            {
                row.ExclusionReason = $"The unit value is given {rate.Unit}, which PRIME cannot match to the sale's area in {Unit(row.LandAreaUnit)}.";
                continue;
            }
            row.Value = Math.Round(area * rate.UnitValue, 2, MidpointRounding.AwayFromZero);
            row.Ratio = ValuationTesting.Ratio(row.Value.Value, row.Price.Value);
        }
        db.ValuationTestRuns.Add(run);
        await db.SaveChangesAsync(ct);
        return await GetAsync(run.Id, ct);
    }

    public async Task<Result<IReadOnlyList<ValuationTestDto>>> ListAsync(Guid? smvId, CancellationToken cancellationToken = default)
    {
        var runs = await Runs().Where(x => smvId == null || x.SmvId == smvId).OrderByDescending(x => x.CreatedAt).Take(200).ToListAsync(cancellationToken);
        var counts = await Counts(runs.Select(x => x.Id).ToList(), cancellationToken);
        return Result.Success<IReadOnlyList<ValuationTestDto>>(runs.Where(Visible)
            .Select(x => ToDto(x, counts.GetValueOrDefault(x.Id))).ToList());
    }

    public async Task<Result<ValuationTestDto>> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var run = await Runs().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (run is null || !Visible(run))
        {
            return Fail<ValuationTestDto>("VALUATION_TEST_NOT_FOUND", "No valuation test was found with the given id.");
        }
        var sales = await SalesQuery(id).ToListAsync(cancellationToken);
        var benchmarks = options.Value;
        return Result.Success(ToDto(run, (sales.Count, sales.Count(s => s.Ratio != null))) with
        {
            Groups = Groups(sales, benchmarks, run.Scope.ToDictionary(s => s.MunicipalityId, s => s.Municipality?.Name ?? "")), Benchmarks = benchmarks,
        });
    }

    public async Task<Result<IReadOnlyList<ValuationTestSaleDto>>> ListSalesAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var run = await GetAsync(id, cancellationToken);
        if (run.IsFailure)
        {
            return Fail<IReadOnlyList<ValuationTestSaleDto>>(run.Code!, run.Message!);
        }
        var municipalities = run.Value.Municipalities.ToDictionary(m => m.Id, m => m.Name);
        var sales = await SalesQuery(id).ToListAsync(cancellationToken);
        return Result.Success<IReadOnlyList<ValuationTestSaleDto>>(sales.Select(s => new ValuationTestSaleDto(
            s.Id, s.MarketTransactionId, s.TransactionDate, municipalities.GetValueOrDefault(s.MunicipalityId), s.Barangay?.Name, s.Classification?.Name,
            s.SubClassification?.Name, s.LandArea, s.LandAreaUnit, s.Price, s.RateUnit, s.UnitValue, s.Value, s.Ratio, s.ExclusionReason)).ToList());
    }

    /// <summary>
    /// Statistics of the tested sales by sub-class, class, city/municipality and overall (LAM Book IV p.115: within each
    /// sub-market group), in that order.
    /// </summary>
    public static IReadOnlyList<ValuationTestGroupDto> Groups(IReadOnlyList<ValuationTestSale> sales, ValuationTestingOptions benchmarks,
        IReadOnlyDictionary<Guid, string> municipalityNames)
    {
        var tested = sales.Where(s => s.Ratio != null).ToList();
        var groups = new List<ValuationTestGroupDto>();
        void Add(string level, string name, IEnumerable<ValuationTestSale> rows)
        {
            var (count, median, cod) = ValuationTesting.Statistics(rows.Select(s => s.Ratio!.Value));
            bool? medianOk = median is not { } m || benchmarks.MedianRatioLow is null && benchmarks.MedianRatioHigh is null ? null
                : (benchmarks.MedianRatioLow is not { } low || m >= low) && (benchmarks.MedianRatioHigh is not { } high || m <= high);
            bool? codOk = cod is { } c && benchmarks.MaximumCoefficientOfDispersion is { } max ? c <= max : null;
            groups.Add(new ValuationTestGroupDto(level, name, count, median, cod, medianOk, codOk));
        }
        foreach (var g in tested.GroupBy(s => (Class: s.Classification?.Name ?? "", Sub: s.SubClassification?.Name)).OrderBy(g => g.Key.Class).ThenBy(g => g.Key.Sub))
        {
            Add("SubClass", g.Key.Sub is null ? $"{g.Key.Class} (no sub-class)" : $"{g.Key.Class} — {g.Key.Sub}", g);
        }
        foreach (var g in tested.GroupBy(s => s.Classification?.Name ?? "").OrderBy(g => g.Key))
        {
            Add("Class", g.Key, g);
        }
        foreach (var g in tested.GroupBy(s => municipalityNames.GetValueOrDefault(s.MunicipalityId, "")).OrderBy(g => g.Key))
        {
            Add("Municipality", g.Key, g);
        }
        Add("All", "All tested sales", tested);
        return groups;
    }

    /// <summary>The sale's area in the unit its unit value is given per; null when the unit is not one PRIME recognises.</summary>
    public static decimal? AreaIn(string rateUnit, decimal area, AreaMeasure areaUnit)
    {
        var per = RateUnitOf(rateUnit);
        return per switch
        {
            null => null,
            _ when per == areaUnit => area,
            AreaMeasure.SquareMetre => area * ValuationTesting.SquareMetresPerHectare,
            _ => area / ValuationTesting.SquareMetresPerHectare,
        };
    }

    /// <summary>The area an SMV row's unit is given per, read from its text ("per sqm", "per hectare" …); null when not one PRIME recognises.</summary>
    public static AreaMeasure? RateUnitOf(string rateUnit)
    {
        var unit = rateUnit.Trim().ToLowerInvariant();
        return unit.Contains("hectare") || unit.EndsWith(" ha") || unit == "ha" || unit.Contains("/ha") ? AreaMeasure.Hectare
            : unit.Contains("sqm") || unit.Contains("sq. m") || unit.Contains("sq.m") || unit.Contains("square met") || unit.Contains("m2") || unit.Contains("m²")
                ? AreaMeasure.SquareMetre : null;
    }

    private static string Unit(AreaMeasure unit) => unit == AreaMeasure.Hectare ? "hectares" : "square metres";

    private IQueryable<ValuationTestRun> Runs() =>
        db.ValuationTestRuns.AsNoTracking().Include(x => x.Smv).Include(x => x.Scope).ThenInclude(s => s.Municipality);

    private IQueryable<ValuationTestSale> SalesQuery(Guid id) => db.ValuationTestSales.AsNoTracking()
        .Include(x => x.Barangay).Include(x => x.Classification).Include(x => x.SubClassification)
        .Where(x => x.ValuationTestRunId == id).OrderBy(x => x.TransactionDate).ThenBy(x => x.Id);

    private async Task<Dictionary<Guid, (int Sales, int Tested)>> Counts(List<Guid> ids, CancellationToken ct) =>
        (await db.ValuationTestSales.AsNoTracking().Where(x => ids.Contains(x.ValuationTestRunId))
            .GroupBy(x => x.ValuationTestRunId).Select(g => new { g.Key, Sales = g.Count(), Tested = g.Count(s => s.Ratio != null) }).ToListAsync(ct))
        .ToDictionary(x => x.Key, x => (x.Sales, x.Tested));

    private bool Visible(ValuationTestRun run) => run.Scope.Any(s => jurisdiction.Allows(s.MunicipalityId));

    private static ValuationTestDto ToDto(ValuationTestRun x, (int Sales, int Tested) counts) => new(
        x.Id, x.SmvId, x.Smv?.Reference ?? "", x.Smv?.RevisionYear ?? 0, x.AsOf, x.SalesFrom, x.SalesTo, x.Description,
        x.Scope.Select(s => new SmvSimulationMunicipalityDto(s.MunicipalityId, s.Municipality?.Name ?? "")).OrderBy(m => m.Name).ToList(),
        counts.Sales, counts.Tested, x.CreatedAt);

    private static Result<T> Fail<T>(string code, string message) => Result.Failure<T>(code, message);
}

/// <summary>The testing report of a valuation test: its groups and every sale, as frozen when the test was made (no party names).</summary>
public sealed class ValuationTestFormDataProvider(IApplicationDbContext db, IOptions<ValuationTestingOptions> options) : IFormDataProvider
{
    public FormSubjectType SubjectType => FormSubjectType.ValuationTest;

    public async Task<FormSubjectData?> BuildAsync(Guid subjectId, CancellationToken cancellationToken)
    {
        var ct = cancellationToken;
        var run = await db.ValuationTestRuns.AsNoTracking().Include(x => x.Smv).Include(x => x.Scope).ThenInclude(s => s.Municipality)
            .FirstOrDefaultAsync(x => x.Id == subjectId, ct);
        if (run is null)
        {
            return null;
        }
        var sales = await db.ValuationTestSales.AsNoTracking().Include(x => x.Barangay).Include(x => x.Classification).Include(x => x.SubClassification)
            .Where(x => x.ValuationTestRunId == subjectId).OrderBy(x => x.TransactionDate).ThenBy(x => x.Id).ToListAsync(ct);
        var names = run.Scope.ToDictionary(s => s.MunicipalityId, s => s.Municipality?.Name ?? "");
        var benchmarks = options.Value;
        var groups = ValuationTestService.Groups(sales, benchmarks, names);
        var data = FormData.ToJson(new
        {
            test = new
            {
                smv = run.Smv?.Reference, revisionYear = run.Smv?.RevisionYear, smvStatus = run.Smv?.Status.ToString(), asOf = run.AsOf,
                salesFrom = run.SalesFrom, salesTo = run.SalesTo, description = run.Description,
                scope = names.Values.Order().ToList(), salesCount = sales.Count, testedCount = sales.Count(s => s.Ratio != null),
                benchmarks = new
                {
                    medianLow = benchmarks.MedianRatioLow, medianHigh = benchmarks.MedianRatioHigh, codMax = benchmarks.MaximumCoefficientOfDispersion,
                    configured = benchmarks.MedianRatioLow is not null || benchmarks.MedianRatioHigh is not null || benchmarks.MaximumCoefficientOfDispersion is not null,
                },
            },
            groups = groups.Select(g => new
            {
                level = g.Level switch { "SubClass" => "Sub-class", "Municipality" => "City/municipality", var l => l }, name = g.Name, count = g.Count, median = g.MedianRatio, cod = g.CoefficientOfDispersion,
                medianOk = g.MedianWithinBenchmark, codOk = g.DispersionWithinBenchmark,
            }).ToList(),
            sales = sales.Select(s => new
            {
                date = s.TransactionDate, municipality = names.GetValueOrDefault(s.MunicipalityId), barangay = s.Barangay?.Name,
                classification = s.Classification?.Name, subClass = s.SubClassification?.Name,
                area = s.LandArea, areaUnit = s.LandAreaUnit == AreaMeasure.Hectare ? "ha" : "sqm", price = s.Price, rateUnit = s.RateUnit,
                unitValue = s.UnitValue, value = s.Value, ratio = s.Ratio, excluded = s.ExclusionReason,
            }).ToList(),
        });
        return new FormSubjectData(null, data, null);
    }
}
