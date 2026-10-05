using Microsoft.EntityFrameworkCore;
using Prime.Application.Common;
using Prime.Application.Common.Interfaces;
using Prime.Application.Features.Valuation;
using Prime.Domain.Entities;
using Prime.Domain.Enums;

namespace Prime.Application.Features.SmvSimulations;

public sealed record StartSmvSimulationRequest(Guid SmvId, DateOnly AsOf, IReadOnlyList<Guid>? MunicipalityIds, string? Description);

public sealed record SmvSimulationMunicipalityDto(Guid Id, string Name);

/// <summary>Totals over the units valued and assessed in both the current posted record and the simulation.</summary>
public sealed record SmvSimulationSummaryDto(
    int Simulated, int Failed, int WithoutCurrent, int Compared,
    decimal CurrentMarketValue, decimal SimulatedMarketValue, decimal CurrentAssessedValue, decimal SimulatedAssessedValue,
    decimal SimulatedTaxableAssessedValue, int Higher, int Lower, int Unchanged, int ClassificationChanged);

public sealed record SmvSimulationRunDto(
    Guid Id, Guid SmvId, string SmvReference, int SmvRevisionYear, WorkflowStatus SmvStatus, DateOnly AsOf, string? Description,
    IReadOnlyList<SmvSimulationMunicipalityDto> Municipalities, JobExecutionStatus Status, int TotalCount, int ProcessedCount, int FailedCount,
    DateTimeOffset? StartedAt, DateTimeOffset? CompletedAt, string? Remarks, SmvSimulationSummaryDto? Summary = null);

public sealed record SmvSimulationResultDto(
    Guid Id, Guid RpuId, Guid PropertyId, string Pin, string RpuNumber, RpuType RpuType, string? BarangayName,
    string? CurrentClassification, decimal? CurrentMarketValue, decimal? CurrentAssessedValue,
    string? SimulatedClassification, decimal? SimulatedMarketValue, decimal? SimulatedAssessedValue, decimal? SimulatedTaxableAssessedValue,
    decimal? MarketValueChange, decimal? MarketValueChangePercent, bool ClassificationChanged, string? FailureReason);

public sealed record SmvSimulationResultSearchRequest(
    int Page = 1, int PageSize = 50, Guid? BarangayId = null, bool? Failed = null, bool? ClassificationChanged = null, string? Pin = null);

/// <summary>One assessment row of a simulated unit.</summary>
public sealed record SimulatedAssessmentLineDto(
    int Sequence, string ClassificationName, string ActualUseName, decimal MarketValue, decimal? AssessmentPercentage, decimal AssessedValue,
    Taxability Taxability, string? TaxabilityNote);

/// <summary>One unit valued and assessed under an SMV without storing anything; <see cref="Failure"/> says what stopped it.</summary>
public sealed record SmvUnitSimulationDto(
    Guid RpuId, Guid SmvId, DateOnly AsOf, ValuationDto? Valuation, IReadOnlyList<SimulatedAssessmentLineDto> Lines,
    decimal? MarketValue, decimal? AssessedValue, decimal? TaxableAssessedValue, string? Failure);

public interface ISmvSimulationService
{
    /// <summary>Queues a simulation of the scope under the SMV (a background job).</summary>
    Task<Result<SmvSimulationRunDto>> StartAsync(StartSmvSimulationRequest request, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<SmvSimulationRunDto>>> ListAsync(Guid? smvId, CancellationToken cancellationToken = default);
    Task<Result<SmvSimulationRunDto>> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<PagedResult<SmvSimulationResultDto>>> SearchResultsAsync(Guid id, SmvSimulationResultSearchRequest request, CancellationToken cancellationToken = default);
    /// <summary>Values and assesses one unit under the SMV as of the date, storing nothing.</summary>
    Task<Result<SmvUnitSimulationDto>> SimulateUnitAsync(Guid rpuId, Guid smvId, DateOnly asOf, CancellationToken cancellationToken = default);
}

/// <summary>
/// Simulations of the values a proposed SMV would give (docs/analysis/smv-preparation-general-revision.md §4.3). This
/// service records and queues; <see cref="SmvSimulationRunner"/> does the work. Results are visible within the user's
/// jurisdiction only.
/// </summary>
public sealed class SmvSimulationService(
    IApplicationDbContext db, IClock clock, ICurrentUserService currentUser, IJurisdiction jurisdiction, IBackgroundJobScheduler scheduler,
    SmvSimulator simulator) : ISmvSimulationService
{
    public async Task<Result<SmvSimulationRunDto>> StartAsync(StartSmvSimulationRequest r, CancellationToken cancellationToken = default)
    {
        var municipalities = (r.MunicipalityIds ?? []).Distinct().ToList();
        if (r.AsOf == default || municipalities.Count == 0 || r.Description?.Length > 1000)
        {
            return Fail<SmvSimulationRunDto>("VALIDATION_FAILED", "The SMV, the as-of date and at least one city/municipality are required; description max 1000.");
        }
        if (await db.Municipalities.CountAsync(x => municipalities.Contains(x.Id), cancellationToken) != municipalities.Count)
        {
            return Fail<SmvSimulationRunDto>("MUNICIPALITY_NOT_FOUND", "A city/municipality in the scope does not exist.");
        }
        if (municipalities.Any(m => !jurisdiction.Allows(m)))
        {
            return Fail<SmvSimulationRunDto>(JurisdictionErrors.Code, JurisdictionErrors.Message);
        }
        var smv = await db.Smvs.AsNoTracking().Include(x => x.Coverage).FirstOrDefaultAsync(x => x.Id == r.SmvId, cancellationToken);
        if (smv is null || smv.Status is WorkflowStatus.Rejected or WorkflowStatus.Cancelled or WorkflowStatus.Voided)
        {
            return Fail<SmvSimulationRunDto>("SMV_NOT_FOUND", "The SMV does not exist, or was rejected or cancelled.");
        }
        if (smv.Coverage.Count > 0 && municipalities.Any(m => smv.Coverage.All(c => c.MunicipalityId != m)))
        {
            return Fail<SmvSimulationRunDto>("SMV_NOT_APPLICABLE", "The SMV does not cover every city/municipality in the scope.");
        }
        var run = new SmvSimulationRun
        {
            SmvId = smv.Id, AsOf = r.AsOf, Description = string.IsNullOrWhiteSpace(r.Description) ? null : r.Description.Trim(),
            Scope = municipalities.Select(m => new SmvSimulationScope { MunicipalityId = m }).ToList(),
            Status = JobExecutionStatus.Queued, StartedBy = currentUser.AppUserId, StartedAt = clock.UtcNow,
        };
        db.SmvSimulationRuns.Add(run);
        await db.SaveChangesAsync(cancellationToken);
        scheduler.Enqueue<SmvSimulationRunner>(runner => runner.RunAsync(run.Id, CancellationToken.None));
        return await GetAsync(run.Id, cancellationToken);
    }

    public async Task<Result<IReadOnlyList<SmvSimulationRunDto>>> ListAsync(Guid? smvId, CancellationToken cancellationToken = default)
    {
        var runs = await Runs().Where(x => smvId == null || x.SmvId == smvId).OrderByDescending(x => x.CreatedAt).Take(200).ToListAsync(cancellationToken);
        return Result.Success<IReadOnlyList<SmvSimulationRunDto>>(runs.Where(Visible).Select(x => ToDto(x)).ToList());
    }

    public async Task<Result<SmvSimulationRunDto>> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var run = await Runs().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (run is null || !Visible(run))
        {
            return Fail<SmvSimulationRunDto>("SMV_SIMULATION_NOT_FOUND", "No SMV simulation was found with the given id.");
        }
        // The result rows are filtered to the user's jurisdiction, so are the totals.
        var results = db.SmvSimulationResults.AsNoTracking().Where(x => x.SmvSimulationRunId == id);
        var compared = results.Where(x => x.CurrentAssessedValue != null && x.SimulatedAssessedValue != null);
        var totals = await compared.GroupBy(_ => 1).Select(g => new
        {
            Count = g.Count(),
            CurrentMarket = g.Sum(x => x.CurrentMarketValue ?? 0m), SimulatedMarket = g.Sum(x => x.SimulatedMarketValue ?? 0m),
            CurrentAssessed = g.Sum(x => x.CurrentAssessedValue ?? 0m), SimulatedAssessed = g.Sum(x => x.SimulatedAssessedValue ?? 0m),
            Higher = g.Count(x => x.SimulatedMarketValue > x.CurrentMarketValue), Lower = g.Count(x => x.SimulatedMarketValue < x.CurrentMarketValue),
            ClassificationChanged = g.Count(x => x.CurrentClassificationId != x.SimulatedClassificationId),
        }).FirstOrDefaultAsync(cancellationToken);
        var simulated = await results.CountAsync(x => x.SimulatedAssessedValue != null, cancellationToken);
        var taxable = await results.SumAsync(x => x.SimulatedTaxableAssessedValue ?? 0m, cancellationToken);
        var failed = await results.CountAsync(x => x.FailureReason != null, cancellationToken);
        var withoutCurrent = await results.CountAsync(x => x.CurrentAssessmentId == null, cancellationToken);
        var summary = new SmvSimulationSummaryDto(simulated, failed, withoutCurrent, totals?.Count ?? 0,
            totals?.CurrentMarket ?? 0m, totals?.SimulatedMarket ?? 0m, totals?.CurrentAssessed ?? 0m, totals?.SimulatedAssessed ?? 0m, taxable,
            totals?.Higher ?? 0, totals?.Lower ?? 0, (totals?.Count ?? 0) - (totals?.Higher ?? 0) - (totals?.Lower ?? 0), totals?.ClassificationChanged ?? 0);
        return Result.Success(ToDto(run, summary));
    }

    public async Task<Result<PagedResult<SmvSimulationResultDto>>> SearchResultsAsync(Guid id, SmvSimulationResultSearchRequest r,
        CancellationToken cancellationToken = default)
    {
        var run = await GetAsync(id, cancellationToken);
        if (run.IsFailure)
        {
            return Fail<PagedResult<SmvSimulationResultDto>>(run.Code!, run.Message!);
        }
        var page = Math.Max(1, r.Page);
        var size = Math.Clamp(r.PageSize, 1, 200);
        var query = db.SmvSimulationResults.AsNoTracking().Where(x => x.SmvSimulationRunId == id);
        if (r.BarangayId is { } barangay)
        {
            query = query.Where(x => x.BarangayId == barangay);
        }
        if (r.Failed is { } failed)
        {
            query = failed ? query.Where(x => x.FailureReason != null) : query.Where(x => x.FailureReason == null);
        }
        if (r.ClassificationChanged == true)
        {
            query = query.Where(x => x.CurrentAssessmentId != null && x.SimulatedClassificationId != null && x.CurrentClassificationId != x.SimulatedClassificationId);
        }
        if (!string.IsNullOrWhiteSpace(r.Pin))
        {
            var pin = r.Pin.Trim();
            query = query.Where(x => x.Pin.StartsWith(pin));
        }
        var total = await query.CountAsync(cancellationToken);
        var rows = await query.OrderBy(x => x.Pin).ThenBy(x => x.RpuNumber).Skip((page - 1) * size).Take(size)
            .Select(x => new
            {
                Result = x, Barangay = db.Barangays.Where(b => b.Id == x.BarangayId).Select(b => b.Name).FirstOrDefault(),
                Current = x.CurrentClassification != null ? x.CurrentClassification.Name : null,
                Simulated = x.SimulatedClassification != null ? x.SimulatedClassification.Name : null,
            })
            .ToListAsync(cancellationToken);
        return Result.Success(new PagedResult<SmvSimulationResultDto>
        {
            Items = rows.Select(x => ToDto(x.Result, x.Barangay, x.Current, x.Simulated)).ToList(), TotalCount = total, Page = page, PageSize = size,
        });
    }

    public async Task<Result<SmvUnitSimulationDto>> SimulateUnitAsync(Guid rpuId, Guid smvId, DateOnly asOf, CancellationToken cancellationToken = default)
    {
        if (asOf == default)
        {
            return Fail<SmvUnitSimulationDto>("VALIDATION_FAILED", "The as-of date is required.");
        }
        if (!await db.RealPropertyUnits.AnyAsync(x => x.Id == rpuId, cancellationToken))
        {
            return Fail<SmvUnitSimulationDto>("RPU_NOT_FOUND", "No RPU was found with the given id.");
        }
        var simulation = await simulator.SimulateAsync(rpuId, smvId, asOf, cancellationToken);
        // A refused SMV is a refusal of the request, not a result of the unit.
        if (simulation.Valuation is null && simulation.Failure is { } failure
            && await db.Smvs.AnyAsync(x => x.Id == smvId && x.Status != WorkflowStatus.Rejected && x.Status != WorkflowStatus.Cancelled
                && x.Status != WorkflowStatus.Voided, cancellationToken) is false)
        {
            return Fail<SmvUnitSimulationDto>("PROPOSED_SMV_NOT_FOUND", failure);
        }
        var lines = simulation.Lines ?? [];
        var classificationIds = lines.Select(l => l.ClassificationId).Distinct().ToList();
        var actualUseIds = lines.Select(l => l.ActualUseId).Distinct().ToList();
        var classifications = await db.Classifications.AsNoTracking().Where(x => classificationIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);
        var actualUses = await db.ActualUses.AsNoTracking().Where(x => actualUseIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);
        return Result.Success(new SmvUnitSimulationDto(rpuId, smvId, asOf, simulation.Valuation,
            lines.Select(l => new SimulatedAssessmentLineDto(l.Sequence, classifications.GetValueOrDefault(l.ClassificationId, ""),
                actualUses.GetValueOrDefault(l.ActualUseId, ""), l.MarketValue, l.AssessmentPercentage, l.AssessedValue, l.Taxability, l.TaxabilityNote)).ToList(),
            simulation.MarketValue, simulation.AssessedValue, simulation.TaxableAssessedValue, simulation.Failure));
    }

    private IQueryable<SmvSimulationRun> Runs() =>
        db.SmvSimulationRuns.AsNoTracking().Include(x => x.Smv).Include(x => x.Scope).ThenInclude(s => s.Municipality);

    /// <summary>A run is visible to a user whose jurisdiction includes one of its cities/municipalities.</summary>
    private bool Visible(SmvSimulationRun run) => run.Scope.Any(s => jurisdiction.Allows(s.MunicipalityId));

    private static SmvSimulationRunDto ToDto(SmvSimulationRun x, SmvSimulationSummaryDto? summary = null) => new(
        x.Id, x.SmvId, x.Smv?.Reference ?? "", x.Smv?.RevisionYear ?? 0, x.Smv?.Status ?? WorkflowStatus.Draft, x.AsOf, x.Description,
        x.Scope.Select(s => new SmvSimulationMunicipalityDto(s.MunicipalityId, s.Municipality?.Name ?? "")).OrderBy(m => m.Name).ToList(),
        x.Status, x.TotalCount, x.ProcessedCount, x.FailedCount, x.StartedAt, x.CompletedAt, x.Remarks, summary);

    private static SmvSimulationResultDto ToDto(SmvSimulationResult x, string? barangay, string? current, string? simulated)
    {
        decimal? change = x.CurrentMarketValue is { } c && x.SimulatedMarketValue is { } s ? s - c : null;
        decimal? percent = change is { } d && x.CurrentMarketValue is > 0 ? Math.Round(d / x.CurrentMarketValue.Value * 100m, 2, MidpointRounding.AwayFromZero) : null;
        var changed = x.CurrentAssessmentId != null && x.SimulatedClassificationId != null && x.CurrentClassificationId != x.SimulatedClassificationId;
        return new SmvSimulationResultDto(x.Id, x.RpuId, x.PropertyId, x.Pin, x.RpuNumber, x.RpuType, barangay, current, x.CurrentMarketValue,
            x.CurrentAssessedValue, simulated, x.SimulatedMarketValue, x.SimulatedAssessedValue, x.SimulatedTaxableAssessedValue, change, percent, changed,
            x.FailureReason);
    }

    private static Result<T> Fail<T>(string code, string message) => Result.Failure<T>(code, message);
}
