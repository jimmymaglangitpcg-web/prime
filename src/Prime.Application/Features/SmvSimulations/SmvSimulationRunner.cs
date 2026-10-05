using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Prime.Application.Common.Interfaces;
using Prime.Domain.Entities;
using Prime.Domain.Enums;

namespace Prime.Application.Features.SmvSimulations;

/// <summary>
/// Runs a simulation (docs/analysis/smv-preparation-general-revision.md §4.3), invoked by the background job scheduler:
/// every active unit with a current Tax Declaration in the scope, in PIN order, valued and assessed under the SMV as of
/// the run's date without storing a valuation or an assessment. A unit that cannot be valued or assessed is recorded
/// with the reason and the run goes on. Results are saved in batches; a stopped run keeps what it saved.
/// </summary>
public sealed class SmvSimulationRunner(
    IApplicationDbContext db, ICurrentUserService currentUser, SmvSimulator simulator, IClock clock, ILogger<SmvSimulationRunner> logger)
{
    private const int Batch = 100;

    public async Task RunAsync(Guid runId, CancellationToken cancellationToken)
    {
        var run = await db.SmvSimulationRuns.Include(x => x.Scope).FirstOrDefaultAsync(x => x.Id == runId, cancellationToken);
        if (run is null || run.Status != JobExecutionStatus.Queued)
        {
            return;
        }
        currentUser.ActAsForBackgroundJob(run.StartedBy);
        run.Status = JobExecutionStatus.Running;
        await db.SaveChangesAsync(cancellationToken);
        var (smvId, asOf) = (run.SmvId, run.AsOf);
        var municipalities = run.Scope.Select(s => s.MunicipalityId).ToList();
        try
        {
            var units = await db.RealPropertyUnits.AsNoTracking().IgnoreQueryFilters()
                .Where(u => u.Status == RecordStatus.Active && municipalities.Contains(u.Property!.MunicipalityId)
                    && u.Property.Status == RecordStatus.Active
                    && db.TaxDeclarations.Any(t => t.RpuId == u.Id && t.Status == WorkflowStatus.Approved))
                .Select(u => new
                {
                    u.Id, u.PropertyId, u.RpuNumber, u.RpuType, u.Property!.MunicipalityId, u.Property.BarangayId, Pin = u.Property.PropertyIdentificationNumber,
                    Current = db.Assessments.Where(a => a.RpuId == u.Id && a.Status == WorkflowStatus.Posted)
                        .OrderByDescending(a => a.EffectiveDate).ThenByDescending(a => a.PostedAt)
                        .Select(a => new
                        {
                            a.Id, a.MarketValue, a.AssessedValue,
                            Principal = a.Lines.OrderByDescending(l => l.MarketValue).ThenBy(l => l.Sequence).Select(l => (Guid?)l.ClassificationId).FirstOrDefault(),
                        })
                        .FirstOrDefault(),
                })
                .OrderBy(u => u.Pin).ThenBy(u => u.RpuNumber)
                .ToListAsync(cancellationToken);
            run.TotalCount = units.Count;
            await db.SaveChangesAsync(cancellationToken);

            var (processed, failed) = (0, 0);
            foreach (var batch in units.Chunk(Batch))
            {
                var results = new List<SmvSimulationResult>();
                foreach (var u in batch)
                {
                    var result = new SmvSimulationResult
                    {
                        SmvSimulationRunId = runId, RpuId = u.Id, PropertyId = u.PropertyId, MunicipalityId = u.MunicipalityId, BarangayId = u.BarangayId,
                        Pin = u.Pin, RpuNumber = u.RpuNumber, RpuType = u.RpuType,
                        CurrentAssessmentId = u.Current?.Id, CurrentMarketValue = u.Current?.MarketValue, CurrentAssessedValue = u.Current?.AssessedValue,
                        CurrentClassificationId = u.Current?.Principal,
                    };
                    try
                    {
                        var simulation = await simulator.SimulateAsync(u.Id, smvId, asOf, cancellationToken);
                        (result.SimulatedMarketValue, result.SimulatedAssessedValue, result.SimulatedTaxableAssessedValue, result.SimulatedClassificationId) =
                            (simulation.MarketValue, simulation.AssessedValue, simulation.TaxableAssessedValue, simulation.PrincipalClassificationId);
                        result.FailureReason = Truncate(simulation.Failure);
                        result.Lines = (simulation.Lines ?? []).Select(l => new SmvSimulationResultLine
                        {
                            Sequence = l.Sequence, ClassificationId = l.ClassificationId, ActualUseId = l.ActualUseId, MarketValue = l.MarketValue,
                            AssessedValue = l.AssessedValue, Taxable = l.Taxability == Taxability.Taxable,
                        }).ToList();
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        logger.LogError(ex, "SMV simulation {RunId}: unit {RpuId} failed", runId, u.Id);
                        result.FailureReason = "An unexpected error stopped this unit's valuation; see the server log.";
                    }
                    result.ProcessedAt = clock.UtcNow;
                    results.Add(result);
                    processed++;
                    failed += result.FailureReason is null ? 0 : 1;
                }
                // The computed valuations loaded the units' records; nothing of them is to be saved.
                db.ClearChangeTracker();
                db.SmvSimulationResults.AddRange(results);
                run = await db.SmvSimulationRuns.FirstAsync(x => x.Id == runId, cancellationToken);
                (run.ProcessedCount, run.FailedCount) = (processed, failed);
                await db.SaveChangesAsync(cancellationToken);
            }
            run.Status = run.TotalCount > 0 && run.FailedCount == run.TotalCount ? JobExecutionStatus.Failed : JobExecutionStatus.Completed;
            run.Remarks = run.TotalCount == 0 ? "No unit in scope has a current Tax Declaration."
                : $"{run.TotalCount - run.FailedCount} of {run.TotalCount} unit(s) simulated."
                    + (run.FailedCount > 0 ? $" {run.FailedCount} could not be valued or assessed; see their reasons." : "");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "SMV simulation {RunId} stopped", runId);
            db.ClearChangeTracker();
            run = await db.SmvSimulationRuns.FirstAsync(x => x.Id == runId, cancellationToken);
            run.Status = JobExecutionStatus.Failed;
            run.Remarks = "The run stopped on an unexpected error; the results saved so far are kept. Start another simulation.";
        }
        run.CompletedAt = clock.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    private static string? Truncate(string? s) => s is { Length: > 1000 } ? s[..1000] : s;
}
