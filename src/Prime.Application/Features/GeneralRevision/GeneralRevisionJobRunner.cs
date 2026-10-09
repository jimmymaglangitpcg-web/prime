using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Prime.Application.Common;
using Prime.Application.Common.Interfaces;
using Prime.Application.Features.Assessments;
using Prime.Application.Features.Valuation;
using Prime.Domain.Entities;
using Prime.Domain.Enums;

namespace Prime.Application.Features.GeneralRevision;

/// <summary>
/// The actual General Revision batch work (CLAUDE.md §33/§72:
/// SELECT = the caller-supplied <c>rpuIds</c>; PREVIEW+CALCULATE = this
/// method producing Draft Assessments; VALIDATE = per-RPU failures recorded
/// without aborting the batch; COMPARE/REVIEW/APPROVE/POST = the normal
/// per-Assessment workflow endpoints, filterable by <c>RevisionReference</c>).
///
/// Invoked by Hangfire, not called directly from a controller — Hangfire's
/// ASP.NET Core integration resolves this class from a fresh DI scope per
/// job execution (the same way a controller gets a fresh scope per HTTP
/// request), so its constructor-injected services are already correctly
/// scoped without this class needing to manage scopes itself. Since that
/// scope has no HTTP request behind it, nothing populates
/// <see cref="ICurrentUserService"/> the normal way — this is why the very
/// first thing <see cref="RunAsync"/> does is call
/// <see cref="ICurrentUserService.ActAsForBackgroundJob"/>, so
/// <c>CreatedBy</c> on every row this job produces reflects who started
/// the revision (required for maker-checker to mean anything on them later).
/// </summary>
public sealed class GeneralRevisionJobRunner(
    IApplicationDbContext db,
    ICurrentUserService currentUser,
    IValuationService valuationService,
    IAssessmentService assessmentService,
    IClock clock,
    ILogger<GeneralRevisionJobRunner> logger)
{
    public async Task RunAsync(Guid jobId, IReadOnlyList<Guid> rpuIds, int revisionYear, CancellationToken cancellationToken)
    {
        var job = await db.GeneralRevisionJobs.FirstOrDefaultAsync(x => x.Id == jobId, cancellationToken);
        if (job is null)
        {
            return;
        }

        currentUser.ActAsForBackgroundJob(job.StartedBy);
        job.Status = JobExecutionStatus.Running;
        await db.SaveChangesAsync(cancellationToken);

        foreach (var rpuId in rpuIds)
        {
            try
            {
                await ProcessRpuAsync(job, rpuId, revisionYear, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // One unit's error is that unit's failure; the batch goes on (§33 VALIDATE). What it left unsaved is dropped.
                logger.LogError(ex, "General revision job {JobId}: unit {RpuId} failed", jobId, rpuId);
                db.ClearChangeTracker();
                job = await db.GeneralRevisionJobs.FirstAsync(x => x.Id == jobId, cancellationToken);
                job.FailedCount++;
            }
            job.ProcessedCount++;
            await db.SaveChangesAsync(cancellationToken);
            // A tracker that keeps every unit's rows makes each save slower than the last (production-hardening.md §9, H4).
            db.ClearChangeTracker();
            job = await db.GeneralRevisionJobs.FirstAsync(x => x.Id == jobId, cancellationToken);
        }

        job.Status = job.TotalCount > 0 && job.FailedCount == job.TotalCount ? JobExecutionStatus.Failed : JobExecutionStatus.Completed;
        job.CompletedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task ProcessRpuAsync(Domain.Entities.GeneralRevisionJob job, Guid rpuId, int revisionYear, CancellationToken cancellationToken)
    {
        var rpu = await db.RealPropertyUnits.FirstOrDefaultAsync(x => x.Id == rpuId, cancellationToken);
        if (rpu is null)
        {
            job.FailedCount++;
            return;
        }

        // Valued and assessed as of the revision's effectivity, under the SMV in force then (valuation-foundation.md §4.1).
        var effectiveDate = job.EffectiveDate ?? clock.Today;
        var valuationResult = await valuationService.ComputeForRpuAsync(rpu.Id, cancellationToken, effectiveDate, generalRevision: true);
        if (valuationResult.IsFailure)
        {
            job.FailedCount++;
            return;
        }

        var previousAssessment = await db.Assessments
            .Where(x => x.RpuId == rpuId && x.Status == WorkflowStatus.Posted)
            .OrderByDescending(x => x.EffectiveDate)
            .FirstOrDefaultAsync(cancellationToken);

        var assessmentResult = await assessmentService.CreateAsync(
            new CreateAssessmentRequest(
                valuationResult.Value.Id,
                revisionYear,
                effectiveDate,
                previousAssessment?.Id,
                job.Id,
                $"General Revision {revisionYear}"),
            cancellationToken);

        if (assessmentResult.IsFailure)
        {
            job.FailedCount++;
        }
    }
}
