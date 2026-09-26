using Microsoft.EntityFrameworkCore;
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
    IClock clock)
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
            await ProcessRpuAsync(job, rpuId, revisionYear, cancellationToken);
            job.ProcessedCount++;
            await db.SaveChangesAsync(cancellationToken);
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

        var valuationResult = await valuationService.ComputeForRpuAsync(rpu.Id, cancellationToken);
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
                clock.Today,
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
