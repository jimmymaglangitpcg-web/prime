using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Prime.Application.Common;
using Prime.Application.Common.Interfaces;
using Prime.Domain.Enums;

namespace Prime.Application.Features.GeneralRevision;

/// <summary>
/// Starts a General Revision as a background job (CLAUDE.md §33/§72) —
/// returns as soon as the job is queued, never blocking the HTTP request
/// that started it. <see cref="GeneralRevisionJobRunner"/> does the actual
/// per-RPU work.
/// </summary>
public sealed class GeneralRevisionService(
    IApplicationDbContext db,
    IValidator<StartGeneralRevisionRequest> validator,
    ICurrentUserService currentUser,
    IBackgroundJobScheduler backgroundJobScheduler) : IGeneralRevisionService
{
    public async Task<Result<GeneralRevisionJobDto>> StartAsync(StartGeneralRevisionRequest request, CancellationToken cancellationToken = default)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure<GeneralRevisionJobDto>("VALIDATION_FAILED", string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)));
        }

        var job = new Domain.Entities.GeneralRevisionJob
        {
            RevisionYear = request.RevisionYear,
            Status = JobExecutionStatus.Queued,
            TotalCount = request.RpuIds.Count,
            StartedBy = currentUser.AppUserId,
            StartedAt = DateTimeOffset.UtcNow,
        };

        db.GeneralRevisionJobs.Add(job);
        await db.SaveChangesAsync(cancellationToken);

        backgroundJobScheduler.Enqueue<GeneralRevisionJobRunner>(runner => runner.RunAsync(job.Id, request.RpuIds, request.RevisionYear, CancellationToken.None));

        return Result.Success(ToDto(job));
    }

    public async Task<Result<GeneralRevisionJobDto>> GetStatusAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        var job = await db.GeneralRevisionJobs.FirstOrDefaultAsync(x => x.Id == jobId, cancellationToken);
        return job is null
            ? Result.Failure<GeneralRevisionJobDto>("GENERAL_REVISION_JOB_NOT_FOUND", "No General Revision job was found with the given id.")
            : Result.Success(ToDto(job));
    }

    private static GeneralRevisionJobDto ToDto(Domain.Entities.GeneralRevisionJob x) => new(
        x.Id, x.RevisionYear, x.Status, x.TotalCount, x.ProcessedCount, x.FailedCount, x.StartedBy, x.StartedAt, x.CompletedAt, x.Remarks);
}
