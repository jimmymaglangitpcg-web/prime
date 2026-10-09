using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Prime.Application.Common.Interfaces;
using Prime.Application.Common;
using Prime.Application.Features.Assessments;
using Prime.Application.Features.Notices;
using Prime.Application.Features.Properties;
using Prime.Application.Features.TaxDeclarations;
using Prime.Application.Features.Valuation;
using Prime.Domain.Entities;
using Prime.Domain.Exceptions;
using Prime.Domain.Enums;

namespace Prime.Application.Features.GeneralRevision;

/// <summary>
/// The runs of a general revision programme (docs/analysis/smv-preparation-general-revision.md §4.6), invoked by the
/// background job scheduler. A Compile run turns every active unit with a current Tax Declaration in the scope into an
/// item with its posted assessment (GRI 3–5). A Value run values each chosen item as of the revision's effectivity under
/// the revision's SMV and drafts its assessment (GRI 12–13), in PIN order; an item it cannot value is marked Failed with
/// the reason and the run goes on. Each item is saved as it is done, so a stopped run resumes where it left off.
/// A batch action (submit, approve, reject, post; submit or approve the Tax Declarations) calls the ordinary single-record
/// action for each item in PIN order, as the user who started the run, so the approval chain, maker-checker and every
/// other check hold per item (Q14); an item it cannot process is recorded as a <see cref="GeneralRevisionRunIssue"/>.
/// </summary>
public sealed class GeneralRevisionProgrammeRunner(
    IApplicationDbContext db, ICurrentUserService currentUser, IValuationService valuation, IAssessmentService assessments,
    ITaxDeclarationService taxDeclarations, INoticeService notices, IClock clock, ILogger<GeneralRevisionProgrammeRunner> logger)
{
    private const int CompileBatch = 500;
    /// <summary>Items of the current batch run done with a note (one runner per job execution).</summary>
    private int notes;

    public async Task RunAsync(Guid jobId, IReadOnlyList<Guid> itemIds, CancellationToken cancellationToken)
    {
        var job = await db.GeneralRevisionJobs.FirstOrDefaultAsync(x => x.Id == jobId, cancellationToken);
        if (job?.GeneralRevisionProgrammeId is not { } programmeId)
        {
            return;
        }
        // Run again by the scheduler (it re-queues the job of a server that stopped mid-run, and retries): a finished run is
        // not repeated, and a run that was under way resumes instead of starting over (production-hardening.md §9, H4).
        if (job.Status is JobExecutionStatus.Completed or JobExecutionStatus.Failed)
        {
            return;
        }
        var resumed = job.Status == JobExecutionStatus.Running;
        currentUser.ActAsForBackgroundJob(job.StartedBy);
        job.Status = JobExecutionStatus.Running;
        await db.SaveChangesAsync(cancellationToken);
        var programme = await db.GeneralRevisionProgrammes.AsNoTracking().Include(x => x.Scope).FirstAsync(x => x.Id == programmeId, cancellationToken);
        if (resumed)
        {
            (job, itemIds) = await ResumeAsync(job, programmeId, itemIds, cancellationToken);
        }
        try
        {
            if (job.Mode == GeneralRevisionRunMode.Compile)
            {
                job = await CompileAsync(job, programme, cancellationToken);
            }
            else if (job.Mode == GeneralRevisionRunMode.Value)
            {
                foreach (var itemId in itemIds)
                {
                    job = await ValueAsync(job, programme, itemId, cancellationToken);
                    job.ProcessedCount++;
                    await db.SaveChangesAsync(cancellationToken);
                    job = await ReleaseAsync(job.Id, cancellationToken);
                }
            }
            else
            {
                if (job.Mode == GeneralRevisionRunMode.GenerateNotices)
                {
                    job = await GenerateNoticesAsync(job, itemIds, cancellationToken);
                }
                else if (job.Mode == GeneralRevisionRunMode.IssueNotices)
                {
                    job = await IssueNoticesAsync(job, itemIds, cancellationToken);
                }
                else
                {
                    foreach (var itemId in itemIds)
                    {
                        job = await ActAsync(job, itemId, cancellationToken);
                        job.ProcessedCount++;
                        await db.SaveChangesAsync(cancellationToken);
                        job = await ReleaseAsync(job.Id, cancellationToken);
                    }
                }
                var done = job.ProcessedCount - job.FailedCount;
                job.Remarks = $"{done} of {job.TotalCount} item(s) done."
                    + (job.FailedCount > 0 ? $" {job.FailedCount} could not be processed; see the run's issues." : "")
                    + (notes > 0 ? $" {notes} done with a note; see the run's issues." : "");
            }
            job.Status = job.TotalCount > 0 && job.FailedCount == job.TotalCount ? JobExecutionStatus.Failed : JobExecutionStatus.Completed;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "General revision run {JobId} stopped", jobId);
            db.ClearChangeTracker();
            job = await db.GeneralRevisionJobs.FirstAsync(x => x.Id == jobId, cancellationToken);
            job.Status = JobExecutionStatus.Failed;
            job.Remarks = "The run stopped on an unexpected error; the items done so far are kept. Start another run to continue.";
        }
        job.CompletedAt = clock.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Forgets what the last item loaded and wrote, and returns the job tracked again. Without it the change tracker grows
    /// with every item, and each save scans all of it: on a 3,200-unit DEMO run the rate fell from 311 to 74 units a minute
    /// within ten minutes (production-hardening.md §9, H4).
    /// </summary>
    private async Task<GeneralRevisionJob> ReleaseAsync(Guid jobId, CancellationToken ct)
    {
        db.ClearChangeTracker();
        return await db.GeneralRevisionJobs.FirstAsync(x => x.Id == jobId, ct);
    }

    /// <summary>
    /// Where an interrupted run picks up. A value run goes through its list again and skips the items it already finished
    /// (<see cref="ValueAsync"/>), so its counts start again from zero. A batch action keeps only the items still in the state
    /// it acts on; the others were done before the interruption, or refused with an issue, which is counted again.
    /// </summary>
    private async Task<(GeneralRevisionJob Job, IReadOnlyList<Guid> ItemIds)> ResumeAsync(GeneralRevisionJob job, Guid programmeId,
        IReadOnlyList<Guid> itemIds, CancellationToken ct)
    {
        if (job.Mode is not { } mode || mode is GeneralRevisionRunMode.Compile or GeneralRevisionRunMode.Value)
        {
            (job.ProcessedCount, job.FailedCount) = (0, 0);
        }
        else
        {
            var waiting = (await GeneralRevisionProgrammeService.Actionable(db,
                    db.GeneralRevisionItems.IgnoreQueryFilters().Where(x => x.GeneralRevisionProgrammeId == programmeId), mode)
                .Select(x => x.Id).ToListAsync(ct)).ToHashSet();
            var remaining = itemIds.Where(waiting.Contains).ToList();
            job.ProcessedCount = itemIds.Count - remaining.Count;
            job.FailedCount = await db.GeneralRevisionRunIssues.CountAsync(x => x.GeneralRevisionJobId == job.Id && x.Failed, ct);
            itemIds = remaining;
        }
        logger.LogWarning("General revision run {JobId} resumed ({Mode}): {Count} item(s) to go through{Skipping}", job.Id, job.Mode, itemIds.Count,
            job.Mode == GeneralRevisionRunMode.Value ? ", skipping those it finished" : "");
        await db.SaveChangesAsync(ct);
        return (job, itemIds);
    }

    /// <summary>Adds the units in scope not yet in the programme, in batches; the count is what was added.</summary>
    private async Task<GeneralRevisionJob> CompileAsync(GeneralRevisionJob job, GeneralRevisionProgramme programme, CancellationToken ct)
    {
        var municipalities = programme.Scope.Select(s => s.MunicipalityId).ToList();
        var existing = (await db.GeneralRevisionItems.IgnoreQueryFilters().Where(x => x.GeneralRevisionProgrammeId == programme.Id)
            .Select(x => x.RpuId).ToListAsync(ct)).ToHashSet();
        var units = await db.RealPropertyUnits.AsNoTracking().IgnoreQueryFilters()
            .Where(u => u.Status == RecordStatus.Active && municipalities.Contains(u.Property!.MunicipalityId)
                && u.Property.Status == RecordStatus.Active
                && db.TaxDeclarations.Any(t => t.RpuId == u.Id && t.Status == WorkflowStatus.Approved))
            .Select(u => new
            {
                u.Id, u.PropertyId, u.RpuNumber, u.RpuType, u.Property!.MunicipalityId, u.Property.BarangayId, Pin = u.Property.PropertyIdentificationNumber,
                Previous = db.Assessments.Where(a => a.RpuId == u.Id && a.Status == WorkflowStatus.Posted)
                    .OrderByDescending(a => a.EffectiveDate).ThenByDescending(a => a.PostedAt)
                    .Select(a => new { a.Id, a.MarketValue, a.AssessedValue }).FirstOrDefault(),
            })
            .OrderBy(u => u.Pin).ThenBy(u => u.RpuNumber)
            .ToListAsync(ct);
        var adding = units.Where(u => !existing.Contains(u.Id)).ToList();
        job.TotalCount = adding.Count;
        foreach (var batch in adding.Chunk(CompileBatch))
        {
            db.GeneralRevisionItems.AddRange(batch.Select(u => new GeneralRevisionItem
            {
                GeneralRevisionProgrammeId = programme.Id, RpuId = u.Id, PropertyId = u.PropertyId, MunicipalityId = u.MunicipalityId,
                BarangayId = u.BarangayId, Pin = u.Pin, RpuNumber = u.RpuNumber, RpuType = u.RpuType,
                PreviousAssessmentId = u.Previous?.Id, PreviousMarketValue = u.Previous?.MarketValue, PreviousAssessedValue = u.Previous?.AssessedValue,
            }));
            job.ProcessedCount += batch.Length;
            await db.SaveChangesAsync(ct);
            job = await ReleaseAsync(job.Id, ct);
        }
        job.Remarks = adding.Count == 0 ? "Every unit in scope is already compiled." : $"{adding.Count} unit(s) compiled.";
        return job;
    }

    /// <summary>Returns the job as tracked afterwards (reloaded when an unexpected error cleared the change tracker).</summary>
    private async Task<GeneralRevisionJob> ValueAsync(GeneralRevisionJob job, GeneralRevisionProgramme programme, Guid itemId, CancellationToken ct)
    {
        var item = await db.GeneralRevisionItems.IgnoreQueryFilters().Include(x => x.Assessment).FirstOrDefaultAsync(x => x.Id == itemId, ct);
        if (item is null)
        {
            job.FailedCount++;
            return job;
        }
        // Finished by this run before it was interrupted. One it had only reset (still Pending) is valued again.
        if (item.LastRunId == job.Id && item.Status != GeneralRevisionItemStatus.Pending)
        {
            job.FailedCount += item.Status == GeneralRevisionItemStatus.Failed ? 1 : 0;
            return job;
        }
        // A draft from an earlier run gives way to the new one; anything already in review is left alone.
        if (item.Assessment is { } earlier)
        {
            if (earlier.Status is not (WorkflowStatus.Draft or WorkflowStatus.Rejected or WorkflowStatus.Cancelled))
            {
                return job;
            }
            if (earlier.Status == WorkflowStatus.Draft)
            {
                earlier.Status = WorkflowStatus.Cancelled;
            }
        }
        // Reset by this run but not finished: the run stopped after drafting the unit's assessment and before saving the item, so
        // that draft is not on the item and would stay behind as a second one (production-hardening.md §9, H4).
        if (item.LastRunId == job.Id)
        {
            var strays = await db.Assessments.Where(a => a.RpuId == item.RpuId && a.RevisionReference == job.Id
                && a.Status == WorkflowStatus.Draft && a.Id != item.AssessmentId).ToListAsync(ct);
            strays.ForEach(a => a.Status = WorkflowStatus.Cancelled);
        }
        // Reset (a Failed item keeps no reason without its status: CK_GeneralRevisionItems_Failed).
        (item.Status, item.AssessmentId, item.ValuationId, item.NewMarketValue, item.NewAssessedValue, item.FailureReason) =
            (GeneralRevisionItemStatus.Pending, null, null, null, null, null);
        item.LastRunId = job.Id;
        item.ProcessedAt = clock.UtcNow;
        await db.SaveChangesAsync(ct);

        try
        {
            var valued = await valuation.ComputeForRpuAsync(item.RpuId, ct, programme.EffectiveDate, generalRevision: true);
            if (valued.IsFailure)
            {
                Fail(job, item, valued.Message ?? valued.Code ?? "The unit could not be valued.");
                return job;
            }
            item.ValuationId = valued.Value.Id;
            // GRI 2: the revision applies its SMV. A unit valued under another one is not assessed.
            if (valued.Value.SmvId is { } used && used != programme.SmvId)
            {
                Fail(job, item, "Valued under another SMV than the revision's: check the revision SMV's coverage and its unit values for this unit.");
                return job;
            }
            // The history continues from the unit's latest posted assessment (refreshed: one may have been posted since compiling).
            var previous = await db.Assessments.AsNoTracking().Where(a => a.RpuId == item.RpuId && a.Status == WorkflowStatus.Posted)
                .OrderByDescending(a => a.EffectiveDate).ThenByDescending(a => a.PostedAt)
                .Select(a => new { a.Id, a.MarketValue, a.AssessedValue }).FirstOrDefaultAsync(ct);
            (item.PreviousAssessmentId, item.PreviousMarketValue, item.PreviousAssessedValue) = (previous?.Id, previous?.MarketValue, previous?.AssessedValue);
            var assessed = await assessments.CreateAsync(new CreateAssessmentRequest(
                valued.Value.Id, programme.RevisionYear, programme.EffectiveDate, previous?.Id, job.Id, $"General Revision {programme.RevisionYear}"), ct);
            if (assessed.IsFailure)
            {
                Fail(job, item, assessed.Message ?? assessed.Code ?? "The unit could not be assessed.");
                return job;
            }
            item.AssessmentId = assessed.Value.Id;
            item.NewMarketValue = assessed.Value.MarketValue;
            item.NewAssessedValue = assessed.Value.AssessedValue;
            item.Status = GeneralRevisionItemStatus.Assessed;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "General revision item {ItemId} failed", itemId);
            db.ClearChangeTracker();
            item = await db.GeneralRevisionItems.IgnoreQueryFilters().FirstAsync(x => x.Id == itemId, ct);
            job = await db.GeneralRevisionJobs.FirstAsync(x => x.Id == job.Id, ct);
            Fail(job, item, ex is DomainException ? ex.Message : "An unexpected error stopped this unit's valuation; see the server log.");
        }
        return job;
    }

    private sealed record NoticeUnit(Guid ItemId, string Pin, string RpuNumber, Guid AssessmentId, Guid PropertyId, Guid RpuId);

    /// <summary>
    /// Drafts the notices the items need (LGC §223), in PIN order: the units of one sole declared owner share one combined
    /// notice (MRPAAO Att. 10); a unit with several owners, or none recorded as owner, gets its own notice addressed to all its
    /// declared parties. A refused notice is an issue on each of its units.
    /// </summary>
    private async Task<GeneralRevisionJob> GenerateNoticesAsync(GeneralRevisionJob job, IReadOnlyList<Guid> itemIds, CancellationToken ct)
    {
        var units = (await db.GeneralRevisionItems.AsNoTracking().IgnoreQueryFilters().Where(x => itemIds.Contains(x.Id) && x.AssessmentId != null)
                .Select(x => new NoticeUnit(x.Id, x.Pin, x.RpuNumber, x.AssessmentId!.Value, x.PropertyId, x.RpuId)).ToListAsync(ct))
            .OrderBy(x => x.Pin).ThenBy(x => x.RpuNumber).ToList();
        var groups = new List<(Guid? Owner, List<NoticeUnit> Units)>();
        foreach (var unit in units)
        {
            var owners = await (await PropertyParties.ScopeAsync(db, unit.PropertyId, unit.RpuId, x => x.IsCurrent, ct))
                .Where(x => x.Role == PropertyPartyRole.Owner).Select(x => x.TaxpayerId).Distinct().ToListAsync(ct);
            Guid? owner = owners.Count == 1 ? owners[0] : null;
            if (owner is not null && groups.FirstOrDefault(g => g.Owner == owner) is { Units: { } list })
            {
                list.Add(unit);
            }
            else
            {
                groups.Add((owner, [unit]));
            }
        }
        foreach (var (owner, group) in groups)
        {
            Result<NoticeDto> result;
            try
            {
                result = owner is { } taxpayerId
                    ? await notices.GenerateCombinedAsync(new GenerateCombinedNoticeRequest(taxpayerId, group.Select(u => u.AssessmentId).ToList()), ct)
                    : await notices.GenerateAsync(new GenerateNoticeRequest(group[0].AssessmentId), ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "General revision run {JobId}: notice generation failed", job.Id);
                result = Result.Failure<NoticeDto>("UNEXPECTED_ERROR", "An unexpected error stopped this notice; see the server log.");
            }
            if (result.IsFailure)
            {
                db.ClearChangeTracker();
                job = await db.GeneralRevisionJobs.FirstAsync(x => x.Id == job.Id, ct);
                foreach (var u in group)
                {
                    Issue(job, u.ItemId, u.Pin, u.RpuNumber, result.Code ?? "FAILED", result.Message ?? "Refused.", failed: true);
                }
            }
            job.ProcessedCount += group.Count;
            await db.SaveChangesAsync(ct);
            job = await ReleaseAsync(job.Id, ct);
        }
        job.ProcessedCount = job.TotalCount;
        return job;
    }

    /// <summary>Issues (and numbers) each draft notice listing the items, in the PIN order of its first unit.</summary>
    private async Task<GeneralRevisionJob> IssueNoticesAsync(GeneralRevisionJob job, IReadOnlyList<Guid> itemIds, CancellationToken ct)
    {
        var drafts = await db.GeneralRevisionItems.AsNoTracking().IgnoreQueryFilters().Where(x => itemIds.Contains(x.Id))
            .SelectMany(x => db.NoticeOfAssessmentItems.Where(i => i.AssessmentId == x.AssessmentId)
                .Join(db.NoticesOfAssessment.Where(n => n.Status == NoticeStatus.Draft), i => i.NoticeOfAssessmentId, n => n.Id,
                    (i, n) => new { NoticeId = n.Id, ItemId = x.Id, x.Pin, x.RpuNumber }))
            .ToListAsync(ct);
        foreach (var notice in drafts.GroupBy(d => d.NoticeId).OrderBy(g => g.Min(d => d.Pin)))
        {
            Result<NoticeDto> result;
            try
            {
                result = await notices.IssueAsync(notice.Key, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "General revision run {JobId}: issuing notice {NoticeId} failed", job.Id, notice.Key);
                result = Result.Failure<NoticeDto>("UNEXPECTED_ERROR", "An unexpected error stopped this notice; see the server log.");
            }
            if (result.IsFailure)
            {
                db.ClearChangeTracker();
                job = await db.GeneralRevisionJobs.FirstAsync(x => x.Id == job.Id, ct);
                foreach (var u in notice)
                {
                    Issue(job, u.ItemId, u.Pin, u.RpuNumber, result.Code ?? "FAILED", result.Message ?? "Refused.", failed: true);
                }
            }
            job.ProcessedCount += notice.Count();
            await db.SaveChangesAsync(ct);
            job = await ReleaseAsync(job.Id, ct);
        }
        job.ProcessedCount = job.TotalCount;
        return job;
    }

    private void Issue(GeneralRevisionJob job, Guid itemId, string pin, string rpuNumber, string code, string message, bool failed)
    {
        if (failed)
        {
            job.FailedCount++;
        }
        else
        {
            notes++;
        }
        db.GeneralRevisionRunIssues.Add(new GeneralRevisionRunIssue
        {
            GeneralRevisionJobId = job.Id, GeneralRevisionItemId = itemId, Pin = pin, RpuNumber = rpuNumber, Failed = failed,
            Code = code.Length <= 100 ? code : code[..100], Message = message.Length <= 1000 ? message : message[..1000],
        });
    }

    /// <summary>One item of a batch action. Returns the job as tracked afterwards: after a refusal the change tracker is cleared, so
    /// nothing the refused action added (an approval record signed before a later check failed) is saved with the next item.</summary>
    private async Task<GeneralRevisionJob> ActAsync(GeneralRevisionJob job, Guid itemId, CancellationToken ct)
    {
        var item = await db.GeneralRevisionItems.AsNoTracking().IgnoreQueryFilters().Where(x => x.Id == itemId)
            .Select(x => new { x.Id, x.Pin, x.RpuNumber, x.AssessmentId }).FirstOrDefaultAsync(ct);
        if (item is null)
        {
            job.FailedCount++;
            return job;
        }
        (string Code, string Message, bool Failed)? outcome;
        try
        {
            outcome = await PerformAsync(job, item.AssessmentId, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "General revision run {JobId}: item {ItemId} failed", job.Id, itemId);
            outcome = ("UNEXPECTED_ERROR", "An unexpected error stopped this item; see the server log.", true);
        }
        if (outcome is not { } r)
        {
            return job;
        }
        if (r.Failed)
        {
            db.ClearChangeTracker();
            job = await db.GeneralRevisionJobs.FirstAsync(x => x.Id == job.Id, ct);
        }
        Issue(job, item.Id, item.Pin, item.RpuNumber, r.Code, r.Message, r.Failed);
        return job;
    }

    /// <summary>Runs the job's action on the item's assessment or its Tax Declaration: null when it succeeded cleanly; otherwise the
    /// refusal (Failed), or a note on an action that succeeded (a post that prepared no Tax Declaration).</summary>
    private async Task<(string Code, string Message, bool Failed)?> PerformAsync(GeneralRevisionJob job, Guid? assessmentId, CancellationToken ct)
    {
        if (assessmentId is not { } id)
        {
            return ("GENERAL_REVISION_ITEM_NOT_ASSESSED", "The item has no assessment.", true);
        }
        if (job.Mode is GeneralRevisionRunMode.SubmitTaxDeclarations or GeneralRevisionRunMode.ApproveTaxDeclarations)
        {
            var td = await GeneralRevisionProgrammeService.Declaring(db, id).Select(t => (Guid?)t.Id).FirstOrDefaultAsync(ct);
            if (td is not { } tdId)
            {
                return ("TAX_DECLARATION_NOT_FOUND", "No Tax Declaration declares the item's assessment; post the assessment first.", true);
            }
            var declared = job.Mode == GeneralRevisionRunMode.SubmitTaxDeclarations
                ? await taxDeclarations.SubmitForReviewAsync(tdId, ct)
                : await taxDeclarations.ApproveAsync(tdId, ct);
            return Refusal(declared);
        }
        var result = job.Mode switch
        {
            GeneralRevisionRunMode.Submit => await assessments.SubmitForReviewAsync(id, ct),
            GeneralRevisionRunMode.Approve => await assessments.ApproveAsync(id, ct),
            GeneralRevisionRunMode.Reject => await assessments.RejectAsync(id, job.Reason ?? "Returned in a general revision batch.", ct),
            GeneralRevisionRunMode.Post => await assessments.PostAsync(id, ct),
            _ => Result.Failure<AssessmentDto>("VALIDATION_FAILED", "Not a batch action."),
        };
        // Posted, but its new TD could not be prepared (no numbering scheme in force, for instance): the TD is prepared by hand.
        if (job.Mode == GeneralRevisionRunMode.Post && result.IsSuccess
            && result.Value.TaxDeclarationNote is { } note && note.StartsWith("No Tax Declaration", StringComparison.Ordinal))
        {
            return ("TAX_DECLARATION_NOT_PREPARED", $"Posted. {note}", false);
        }
        return Refusal(result);
    }

    private static (string Code, string Message, bool Failed)? Refusal<T>(Result<T> result) =>
        result.IsSuccess ? null : (result.Code ?? "FAILED", result.Message ?? result.Code ?? "The action was refused.", true);

    private static void Fail(GeneralRevisionJob job, GeneralRevisionItem item, string reason)
    {
        item.Status = GeneralRevisionItemStatus.Failed;
        item.FailureReason = reason.Length <= 1000 ? reason : reason[..1000];
        job.FailedCount++;
    }
}
