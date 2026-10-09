using Microsoft.EntityFrameworkCore;
using Prime.Application.Common;
using Prime.Application.Common.Interfaces;
using Prime.Application.Features.Forms;
using Prime.Application.Features.Smv;
using Prime.Domain.DomainServices;
using Prime.Domain.Entities;
using Prime.Domain.Enums;

namespace Prime.Application.Features.SmvSimulations;

public sealed record RevenueImpactRateInput(string Label, decimal RatePercent, string Source);
public sealed record RevenueImpactLevelInput(Guid ClassificationId, Guid? ActualUseId, decimal LowerValue, decimal? UpperValue, decimal Percent);
public sealed record RevenueImpactOptionInput(string Name, decimal RatePercent, string? Description, IReadOnlyList<RevenueImpactLevelInput>? Levels);

public sealed record SaveRevenueImpactStudyRequest(
    string Title, Guid SmvSimulationRunId, int Year, DateOnly? ReferenceDate, decimal? ActualCollection, decimal? Discounts, string? CollectionSource,
    bool IncludeAllTaxableUnits, string? Notes, IReadOnlyList<RevenueImpactRateInput>? Rates, IReadOnlyList<RevenueImpactOptionInput>? Options);

public sealed record RevenueImpactRateDto(string Label, decimal RatePercent, string Source);
public sealed record RevenueImpactLevelDto(Guid ClassificationId, string ClassificationName, Guid? ActualUseId, string? ActualUseName, decimal LowerValue,
    decimal? UpperValue, decimal Percent);
public sealed record RevenueImpactOptionDto(int Sequence, string Name, decimal RatePercent, string? Description, IReadOnlyList<RevenueImpactLevelDto> Levels);

/// <summary>One scenario of the tax impact, compared unit by unit with the current tax; <see cref="RowsAtExistingLevel"/> counts rows no option level matched.</summary>
public sealed record TaxImpactScenarioDto(string Key, string Name, decimal RatePercent, TaxImpactSummary Summary, int RowsAtExistingLevel);

public sealed record RevenueImpactStudyDto(
    Guid Id, string Title, Guid SmvSimulationRunId, string SmvReference, int SmvRevisionYear, DateOnly SimulationAsOf, IReadOnlyList<string> Municipalities,
    int Year, DateOnly ReferenceDate, decimal? ActualCollection, decimal? Discounts, string? CollectionSource, bool IncludeAllTaxableUnits, string? Notes,
    IReadOnlyList<RevenueImpactRateDto> Rates, decimal ExistingRatePercent, IReadOnlyList<RevenueImpactOptionDto> Options,
    RevenueCompliance? Compliance, IReadOnlyList<TaxImpactScenarioDto> Scenarios, int UnitsLeftOut, bool Editable, IReadOnlyList<string> Warnings,
    DateTimeOffset CreatedAt, uint RowVersion = 0);

public sealed record RevenueImpactStudySummaryDto(Guid Id, string Title, int Year, string SmvReference, DateOnly SimulationAsOf, int OptionCount, DateTimeOffset CreatedAt);

/// <summary>One unit's tax under the current values, the new values at existing levels and rates, and each option.</summary>
public sealed record TaxImpactUnitDto(
    Guid RpuId, Guid PropertyId, string Pin, string RpuNumber, RpuType RpuType, string? CurrentClassification, string? NewClassification, bool Reclassified,
    decimal CurrentTaxableAssessedValue, decimal NewTaxableAssessedValue, decimal CurrentTax, decimal NewTax, IReadOnlyList<decimal> OptionTaxes);

public sealed record TaxImpactUnitSearch(int Page = 1, int PageSize = 50, string? Change = null, string? Pin = null);

public interface IRevenueImpactStudyService
{
    Task<Result<IReadOnlyList<RevenueImpactStudySummaryDto>>> ListAsync(CancellationToken cancellationToken = default);
    Task<Result<RevenueImpactStudyDto>> CreateAsync(SaveRevenueImpactStudyRequest request, CancellationToken cancellationToken = default);
    /// <summary>Replaces the study's inputs, rates and options (study data, not configuration).</summary>
    Task<Result<RevenueImpactStudyDto>> UpdateAsync(Guid id, SaveRevenueImpactStudyRequest request, CancellationToken cancellationToken = default);
    Task<Result<RevenueImpactStudyDto>> GetAsync(Guid id, CancellationToken cancellationToken = default);
    /// <summary>The units of the tax impact, filtered by change ("higher", "lower", "unchanged", "reclassified") under the new values.</summary>
    Task<Result<PagedResult<TaxImpactUnitDto>>> SearchUnitsAsync(Guid id, TaxImpactUnitSearch request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Revenue compliance and tax impact studies (docs/analysis/smv-preparation-general-revision.md §4.5, Q12, Q18). Prepared by the
/// provincial office with the Treasurer's figures; everything is computed on reading, from the simulation run's frozen results and
/// the posted assessments, by <see cref="RevenueImpactMath"/>.
/// </summary>
public sealed class RevenueImpactStudyService(IApplicationDbContext db, IJurisdiction jurisdiction) : IRevenueImpactStudyService
{
    public const int MaximumOptions = 3;

    public async Task<Result<IReadOnlyList<RevenueImpactStudySummaryDto>>> ListAsync(CancellationToken cancellationToken = default) =>
        Result.Success<IReadOnlyList<RevenueImpactStudySummaryDto>>(await db.RevenueImpactStudies.AsNoTracking().OrderByDescending(x => x.CreatedAt)
            .Select(x => new RevenueImpactStudySummaryDto(x.Id, x.Title, x.Year,
                x.SmvSimulationRun!.Smv!.OrdinanceNumber ?? x.SmvSimulationRun.Smv.CertificationReference ?? ("Proposed SMV " + x.SmvSimulationRun.Smv.RevisionYear),
                x.SmvSimulationRun.AsOf, x.Options.Count, x.CreatedAt))
            .ToListAsync(cancellationToken));

    public async Task<Result<RevenueImpactStudyDto>> CreateAsync(SaveRevenueImpactStudyRequest r, CancellationToken cancellationToken = default)
    {
        var study = new RevenueImpactStudy();
        var applied = await ApplyAsync(study, r, cancellationToken);
        if (applied is not null)
        {
            return Fail(applied.Code!, applied.Message!);
        }
        db.RevenueImpactStudies.Add(study);
        await db.SaveChangesAsync(cancellationToken);
        return await GetAsync(study.Id, cancellationToken);
    }

    public async Task<Result<RevenueImpactStudyDto>> UpdateAsync(Guid id, SaveRevenueImpactStudyRequest r, CancellationToken cancellationToken = default)
    {
        var study = await db.RevenueImpactStudies.Include(x => x.Rates).Include(x => x.Options).ThenInclude(o => o.Levels)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (study is null)
        {
            return Fail("REVENUE_IMPACT_STUDY_NOT_FOUND", "No revenue and tax impact study was found with the given id.");
        }
        // Study data replaced as a whole.
        db.RevenueImpactOptionLevels.RemoveRange(study.Options.SelectMany(o => o.Levels));
        db.RevenueImpactOptions.RemoveRange(study.Options);
        db.RevenueImpactRates.RemoveRange(study.Rates);
        var applied = await ApplyAsync(study, r, cancellationToken);
        if (applied is not null)
        {
            return Fail(applied.Code!, applied.Message!);
        }
        await db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    /// <summary>Validates the request and writes it onto the study (new child rows added through their sets); null when applied.</summary>
    private async Task<Result?> ApplyAsync(RevenueImpactStudy study, SaveRevenueImpactStudyRequest r, CancellationToken ct)
    {
        if (jurisdiction.Restricted)
        {
            return Result.Failure(SmvPreparationService.ForbiddenCode, "The revenue and tax impact study is prepared by the provincial office.");
        }
        var title = r.Title?.Trim();
        var rates = (r.Rates ?? []).ToList();
        var options = (r.Options ?? []).ToList();
        if (string.IsNullOrEmpty(title) || title.Length > 300 || r.Year is < 1990 or > 2200 || r.ActualCollection < 0 || r.Discounts < 0
            || r.CollectionSource?.Length > 500 || r.Notes?.Length > 4000
            || rates.Any(x => string.IsNullOrWhiteSpace(x.Label) || x.Label.Length > 100 || x.RatePercent is < 0 or > 100 || string.IsNullOrWhiteSpace(x.Source) || x.Source.Length > 300))
        {
            return Result.Failure("VALIDATION_FAILED",
                "A title (max 300) and a year are required; amounts not negative; each rate needs a label (max 100), a percent from 0 to 100 and its source (max 300).");
        }
        if (options.Count > MaximumOptions || options.Any(o => string.IsNullOrWhiteSpace(o.Name) || o.Name.Length > 100 || o.RatePercent is < 0 or > 100
                || o.Description?.Length > 1000 || (o.Levels ?? []).Any(l => l.LowerValue < 0 || l.UpperValue <= l.LowerValue || l.Percent is < 0 or > 100)))
        {
            return Result.Failure("VALIDATION_FAILED",
                $"At most {MaximumOptions} options, each with a name (max 100), a rate from 0 to 100 percent and levels from 0 to 100 percent (upper above lower).");
        }
        if ((r.ActualCollection is null) != (r.Discounts is null) || r.ActualCollection is not null && string.IsNullOrWhiteSpace(r.CollectionSource))
        {
            return Result.Failure("COLLECTION_SOURCE_REQUIRED", "Enter the year's collection and the discounts together, with the Treasurer's source.");
        }
        var run = await db.SmvSimulationRuns.AsNoTracking().FirstOrDefaultAsync(x => x.Id == r.SmvSimulationRunId, ct);
        if (run is null || run.Status != JobExecutionStatus.Completed)
        {
            return Result.Failure("SMV_SIMULATION_NOT_COMPLETED", "The study needs a completed simulation of the SMV.");
        }
        var classIds = options.SelectMany(o => o.Levels ?? []).Select(l => l.ClassificationId).Distinct().ToList();
        var useIds = options.SelectMany(o => o.Levels ?? []).Select(l => l.ActualUseId).OfType<Guid>().Distinct().ToList();
        if (await db.Classifications.CountAsync(x => classIds.Contains(x.Id), ct) != classIds.Count
            || await db.ActualUses.CountAsync(x => useIds.Contains(x.Id), ct) != useIds.Count)
        {
            return Result.Failure("CLASSIFICATION_NOT_FOUND", "A level names a class or use that does not exist.");
        }
        (study.Title, study.SmvSimulationRunId, study.Year, study.ReferenceDate) = (title, run.Id, r.Year, r.ReferenceDate ?? new DateOnly(r.Year, 1, 1));
        (study.ActualCollection, study.Discounts, study.CollectionSource) = (r.ActualCollection, r.Discounts, Clean(r.CollectionSource));
        (study.IncludeAllTaxableUnits, study.Notes) = (r.IncludeAllTaxableUnits, Clean(r.Notes));
        foreach (var rate in rates)
        {
            var row = new RevenueImpactRate { RevenueImpactStudyId = study.Id, Label = rate.Label.Trim(), RatePercent = rate.RatePercent, Source = rate.Source.Trim() };
            db.RevenueImpactRates.Add(row);
            study.Rates.Add(row);
        }
        foreach (var (o, i) in options.Select((o, i) => (o, i)))
        {
            var option = new RevenueImpactOption
            {
                RevenueImpactStudyId = study.Id, Sequence = i + 1, Name = o.Name.Trim(), RatePercent = o.RatePercent, Description = Clean(o.Description),
            };
            db.RevenueImpactOptions.Add(option);
            study.Options.Add(option);
            foreach (var l in o.Levels ?? [])
            {
                var level = new RevenueImpactOptionLevel
                {
                    RevenueImpactOptionId = option.Id, ClassificationId = l.ClassificationId, ActualUseId = l.ActualUseId, LowerValue = l.LowerValue,
                    UpperValue = l.UpperValue, Percent = l.Percent,
                };
                db.RevenueImpactOptionLevels.Add(level);
                option.Levels.Add(level);
            }
        }
        return null;
    }

    public async Task<Result<RevenueImpactStudyDto>> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var loaded = await LoadAsync(id, cancellationToken);
        if (loaded is null)
        {
            return Fail("REVENUE_IMPACT_STUDY_NOT_FOUND", "No revenue and tax impact study was found with the given id.");
        }
        var (study, units, leftOut) = loaded.Value;
        var existingRate = study.Rates.Sum(x => x.RatePercent);
        var warnings = new List<string>();
        if (study.Rates.Count == 0)
        {
            warnings.Add("No existing tax rate entered: enter the Treasurer's rates to compute any tax.");
        }
        if (leftOut > 0)
        {
            warnings.Add($"{leftOut} unit(s) of the simulation were left out: they could not be valued or assessed under the SMV.");
        }
        if (units.Any(u => !u.HasLines))
        {
            warnings.Add("The simulation was run before PRIME kept its assessment rows: options apply their levels to each unit's principal class.");
        }
        RevenueCompliance? compliance = null;
        if (study.ActualCollection is { } collected)
        {
            compliance = RevenueImpactMath.Compliance(await TaxableAssessedValueAsync(study, cancellationToken), existingRate, collected, study.Discounts ?? 0m);
        }
        var scenarios = new List<TaxImpactScenarioDto>
        {
            new("new", "New values at the existing levels and rates", existingRate,
                RevenueImpactMath.Summarize(units.Select(u => new UnitTax(u.CurrentTax(existingRate), u.NewTax(existingRate), u.Reclassified)).ToList()), 0),
        };
        foreach (var option in study.Options.OrderBy(o => o.Sequence))
        {
            var taxes = units.Select(u => (Unit: u, Option: u.OptionTax(option))).ToList();
            scenarios.Add(new TaxImpactScenarioDto($"option-{option.Sequence}", option.Name, option.RatePercent,
                RevenueImpactMath.Summarize(taxes.Select(t => new UnitTax(t.Unit.CurrentTax(existingRate), t.Option.Tax, t.Unit.Reclassified)).ToList()),
                taxes.Sum(t => t.Option.RowsAtExistingLevel)));
        }
        var run = study.SmvSimulationRun!;
        return Result.Success(new RevenueImpactStudyDto(
            study.Id, study.Title, run.Id, run.Smv!.Reference is { Length: > 0 } reference ? reference : $"Proposed SMV {run.Smv.RevisionYear}", run.Smv.RevisionYear,
            run.AsOf, run.Scope.Select(s => s.Municipality?.Name ?? "").Order().ToList(), study.Year, study.ReferenceDate, study.ActualCollection, study.Discounts,
            study.CollectionSource, study.IncludeAllTaxableUnits, study.Notes,
            study.Rates.Select(x => new RevenueImpactRateDto(x.Label, x.RatePercent, x.Source)).ToList(), existingRate,
            study.Options.OrderBy(o => o.Sequence).Select(o => new RevenueImpactOptionDto(o.Sequence, o.Name, o.RatePercent, o.Description,
                o.Levels.Select(l => new RevenueImpactLevelDto(l.ClassificationId, l.Classification?.Name ?? "", l.ActualUseId, l.ActualUse?.Name, l.LowerValue, l.UpperValue,
                    l.Percent)).ToList())).ToList(),
            compliance, scenarios, leftOut, !jurisdiction.Restricted, warnings, study.CreatedAt, study.RowVersion));
    }

    public async Task<Result<PagedResult<TaxImpactUnitDto>>> SearchUnitsAsync(Guid id, TaxImpactUnitSearch r, CancellationToken cancellationToken = default)
    {
        var loaded = await LoadAsync(id, cancellationToken);
        if (loaded is null)
        {
            return Result.Failure<PagedResult<TaxImpactUnitDto>>("REVENUE_IMPACT_STUDY_NOT_FOUND", "No revenue and tax impact study was found with the given id.");
        }
        var (study, units, _) = loaded.Value;
        var rate = study.Rates.Sum(x => x.RatePercent);
        var options = study.Options.OrderBy(o => o.Sequence).ToList();
        var rows = units.Select(u => new TaxImpactUnitDto(u.RpuId, u.PropertyId, u.Pin, u.RpuNumber, u.RpuType, u.CurrentClassification, u.NewClassification, u.Reclassified,
                u.CurrentTaxable, u.NewTaxable, u.CurrentTax(rate), u.NewTax(rate), options.Select(o => u.OptionTax(o).Tax).ToList()))
            .Where(x => r.Change switch
            {
                "higher" => x.NewTax > x.CurrentTax,
                "lower" => x.NewTax < x.CurrentTax,
                "unchanged" => x.NewTax == x.CurrentTax,
                "reclassified" => x.Reclassified,
                _ => true,
            })
            .Where(x => string.IsNullOrWhiteSpace(r.Pin) || x.Pin.StartsWith(r.Pin.Trim(), StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x => x.NewTax - x.CurrentTax).ThenBy(x => x.Pin).ToList();
        var page = Math.Max(1, r.Page);
        var size = Math.Clamp(r.PageSize, 1, 200);
        return Result.Success(new PagedResult<TaxImpactUnitDto>
        {
            Items = rows.Skip((page - 1) * size).Take(size).ToList(), TotalCount = rows.Count, Page = page, PageSize = size,
        });
    }

    /// <summary>A unit in the tax impact: its taxable assessed values before and after, and its simulated rows for the options.</summary>
    private sealed record ImpactUnit(
        Guid RpuId, Guid PropertyId, string Pin, string RpuNumber, RpuType RpuType, string? CurrentClassification, string? NewClassification, bool Reclassified,
        decimal CurrentTaxable, decimal NewTaxable, IReadOnlyList<SmvSimulationResultLine> Rows, bool HasLines)
    {
        public decimal CurrentTax(decimal rate) => RevenueImpactMath.Tax(CurrentTaxable, rate);
        public decimal NewTax(decimal rate) => RevenueImpactMath.Tax(NewTaxable, rate);

        /// <summary>The option's levels on each taxable row (the most specific level for its class and use and bracket), at its rate.</summary>
        public (decimal Tax, int RowsAtExistingLevel) OptionTax(RevenueImpactOption option)
        {
            var assessed = 0m;
            var missing = 0;
            foreach (var row in Rows.Where(x => x.Taxable))
            {
                var level = option.Levels
                    .Where(l => l.ClassificationId == row.ClassificationId && (l.ActualUseId == null || l.ActualUseId == row.ActualUseId)
                        && (l.LowerValue == 0 || l.LowerValue < row.MarketValue) && (l.UpperValue == null || row.MarketValue <= l.UpperValue))
                    .OrderByDescending(l => l.ActualUseId != null).FirstOrDefault();
                if (level is null)
                {
                    assessed += row.AssessedValue;
                    missing++;
                }
                else
                {
                    assessed += Math.Round(row.MarketValue * level.Percent / 100m, 2, MidpointRounding.AwayFromZero);
                }
            }
            return (RevenueImpactMath.Tax(assessed, option.RatePercent), missing);
        }
    }

    /// <summary>The study and its units: land units of the run (every unit when the study extends to them) that were valued and assessed, taxable before or after.</summary>
    private async Task<(RevenueImpactStudy Study, List<ImpactUnit> Units, int LeftOut)?> LoadAsync(Guid id, CancellationToken ct)
    {
        var study = await db.RevenueImpactStudies.AsNoTracking().Include(x => x.Rates)
            .Include(x => x.Options).ThenInclude(o => o.Levels).ThenInclude(l => l.Classification)
            .Include(x => x.Options).ThenInclude(o => o.Levels).ThenInclude(l => l.ActualUse)
            .Include(x => x.SmvSimulationRun!).ThenInclude(r => r.Smv)
            .Include(x => x.SmvSimulationRun!).ThenInclude(r => r.Scope).ThenInclude(s => s.Municipality)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (study is null)
        {
            return null;
        }
        var results = await db.SmvSimulationResults.AsNoTracking().Include(x => x.Lines)
            .Where(x => x.SmvSimulationRunId == study.SmvSimulationRunId && (study.IncludeAllTaxableUnits || x.RpuType == RpuType.Land))
            .Select(x => new
            {
                Result = x, Current = x.CurrentClassification != null ? x.CurrentClassification.Name : null,
                New = x.SimulatedClassification != null ? x.SimulatedClassification.Name : null,
            })
            .ToListAsync(ct);
        var leftOut = results.Count(x => x.Result.SimulatedAssessedValue is null);
        var assessmentIds = results.Select(x => x.Result.CurrentAssessmentId).OfType<Guid>().Distinct().ToList();
        var currentTaxable = await db.AssessmentLines.AsNoTracking()
            .Where(l => assessmentIds.Contains(l.AssessmentId) && l.Taxability == Taxability.Taxable)
            .GroupBy(l => l.AssessmentId).Select(g => new { g.Key, Value = g.Sum(l => l.AssessedValue) }).ToDictionaryAsync(x => x.Key, x => x.Value, ct);
        var units = results.Where(x => x.Result.SimulatedAssessedValue is not null).Select(x =>
        {
            var r = x.Result;
            var before = r.CurrentAssessmentId is { } a ? currentTaxable.GetValueOrDefault(a) : 0m;
            var after = r.SimulatedTaxableAssessedValue ?? 0m;
            // A run made before rows were kept: one row of the unit's principal class and its totals.
            IReadOnlyList<SmvSimulationResultLine> rows = r.Lines.Count > 0 ? r.Lines.OrderBy(l => l.Sequence).ToList()
                : r.SimulatedClassificationId is { } c
                    ? [new SmvSimulationResultLine { ClassificationId = c, MarketValue = r.SimulatedMarketValue ?? 0m, AssessedValue = after, Taxable = after > 0 }]
                    : [];
            var reclassified = r.CurrentAssessmentId != null && r.SimulatedClassificationId != null && r.CurrentClassificationId != r.SimulatedClassificationId;
            return new ImpactUnit(r.RpuId, r.PropertyId, r.Pin, r.RpuNumber, r.RpuType, x.Current, x.New, reclassified, before, after, rows, r.Lines.Count > 0);
        }).Where(u => u.CurrentTaxable > 0 || u.NewTaxable > 0).ToList();
        return (study, units, leftOut);
    }

    /// <summary>
    /// The taxable assessed value on the reference date in the run's cities/municipalities: the taxable rows of each active unit's
    /// posted assessment in force then (the base of the tax potential, Book IV p.117 step 1).
    /// </summary>
    private async Task<decimal> TaxableAssessedValueAsync(RevenueImpactStudy study, CancellationToken ct)
    {
        var municipalities = study.SmvSimulationRun!.Scope.Select(s => s.MunicipalityId).ToList();
        var date = study.ReferenceDate;
        var posted = await db.Assessments.AsNoTracking()
            .Where(a => a.Status == WorkflowStatus.Posted && a.EffectiveDate <= date && a.Rpu!.Status == RecordStatus.Active
                && municipalities.Contains(a.Property!.MunicipalityId))
            .Select(a => new { a.Id, a.RpuId, a.EffectiveDate, a.PostedAt })
            .ToListAsync(ct);
        var inForce = posted.GroupBy(a => a.RpuId).Select(g => g.OrderByDescending(a => a.EffectiveDate).ThenByDescending(a => a.PostedAt).First().Id).ToList();
        var total = 0m;
        foreach (var chunk in inForce.Chunk(1000))
        {
            total += await db.AssessmentLines.AsNoTracking().Where(l => chunk.Contains(l.AssessmentId) && l.Taxability == Taxability.Taxable)
                .SumAsync(l => l.AssessedValue, ct);
        }
        return total;
    }

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static Result<RevenueImpactStudyDto> Fail(string code, string message) => Result.Failure<RevenueImpactStudyDto>(code, message);
}

/// <summary>The Revenue and Tax Impact Report (RTIR) of a study: compliance, the scenarios side by side, and the reclassified units.</summary>
public sealed class RevenueImpactFormDataProvider(IRevenueImpactStudyService studies) : IFormDataProvider
{
    public FormSubjectType SubjectType => FormSubjectType.RevenueImpactStudy;

    public async Task<FormSubjectData?> BuildAsync(Guid subjectId, CancellationToken cancellationToken)
    {
        var study = await studies.GetAsync(subjectId, cancellationToken);
        if (study.IsFailure)
        {
            return null;
        }
        var s = study.Value;
        var reclassified = await studies.SearchUnitsAsync(subjectId, new TaxImpactUnitSearch(1, 200, "reclassified"), cancellationToken);
        var data = FormData.ToJson(new
        {
            study = s,
            reclassified = reclassified.IsSuccess ? reclassified.Value.Items : [],
            reclassifiedCount = reclassified.IsSuccess ? reclassified.Value.TotalCount : 0,
        });
        return new FormSubjectData(null, data, null);
    }
}
