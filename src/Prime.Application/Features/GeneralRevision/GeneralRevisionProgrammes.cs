using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Prime.Application.Common;
using Prime.Application.Common.Interfaces;
using Prime.Domain.Entities;
using Prime.Domain.Enums;

namespace Prime.Application.Features.GeneralRevision;

/// <summary>"GeneralRevision" configuration section (docs/analysis/smv-preparation-general-revision.md §4.6).</summary>
public sealed class GeneralRevisionOptions
{
    public const string SectionName = "GeneralRevision";

    /// <summary>Days a local-calamity suspension (and each extension) lasts; the LAM gives 30 (Book IV p.125).</summary>
    public int CalamitySuspensionDays { get; set; } = 30;

    /// <summary>Days after the latest delivery of the notices before the assessment roll is prepared; the LAM gives 60 (GRI 17; Q15).</summary>
    public int RollWaitDays { get; set; } = 60;
}

public sealed record CreateGeneralRevisionRequest(
    int RevisionYear, DateOnly EffectiveDate, Guid SmvId, IReadOnlyList<Guid> MunicipalityIds, string? OfficeOrderReference, string? OrdinanceReference,
    string? Description);

public sealed record UpdateGeneralRevisionReferencesRequest(string? OfficeOrderReference, string? OrdinanceReference, string? Description);

/// <param name="ItemIds">A Value run: these items only (re-run of chosen ones, including Assessed items whose assessment is still a Draft).
/// Omitted: every Pending item, and Failed ones when <paramref name="IncludeFailed"/>. A batch action: the chosen items that are in the
/// state it acts on; omitted, every such item (of <paramref name="BarangayId"/> when given).</param>
/// <param name="Reason">A Reject run: required, recorded on each assessment returned.</param>
public sealed record StartGeneralRevisionRunRequest(
    GeneralRevisionRunMode Mode, IReadOnlyList<Guid>? ItemIds = null, bool IncludeFailed = true, Guid? BarangayId = null, string? Reason = null);

/// <param name="InspectorId">The appraiser; null clears the assignment.</param>
public sealed record AssignGeneralRevisionInspectionRequest(IReadOnlyList<Guid> ItemIds, Guid? InspectorId, string? Route);

/// <param name="FoundChanges">The inspection found changes (made through the ordinary screens): the item goes back to Pending for the next value run.</param>
public sealed record RecordGeneralRevisionInspectionRequest(DateOnly InspectedOn, string? Notes, bool FoundChanges);

public sealed record GeneralRevisionRunIssueDto(Guid ItemId, string Pin, string RpuNumber, string Code, string Message, bool Failed);

public sealed record SuspendGeneralRevisionRequest(GeneralRevisionSuspensionKind Kind, DateOnly FromDate, string Reference, string? Remarks);

public sealed record LiftGeneralRevisionSuspensionRequest(DateOnly LiftedOn);

public sealed record CancelGeneralRevisionRequest(string Reason);

public sealed record GeneralRevisionSuspensionDto(
    Guid Id, GeneralRevisionSuspensionKind Kind, DateOnly FromDate, DateOnly? UntilDate, string Reference, string? Remarks, DateOnly? LiftedOn, bool InForce);

public sealed record GeneralRevisionRunDto(
    Guid Id, GeneralRevisionRunMode? Mode, JobExecutionStatus Status, int TotalCount, int ProcessedCount, int FailedCount, DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt, string? Remarks, string? Reason = null, int IssueCount = 0);

/// <param name="ItemsByStatus">Items by run outcome.</param>
/// <param name="AssessmentsByStatus">The items' current assessments by workflow status (Draft, PendingReview, Approved, Posted …).</param>
public sealed record GeneralRevisionDto(
    Guid Id, int RevisionYear, DateOnly EffectiveDate, Guid SmvId, string SmvReference, string? OfficeOrderReference, string? OrdinanceReference,
    string? Description, GeneralRevisionStatus Status, IReadOnlyList<GeneralRevisionScopeDto> Scope, int ItemCount,
    IReadOnlyDictionary<string, int> ItemsByStatus, IReadOnlyDictionary<string, int> AssessmentsByStatus,
    decimal PreviousMarketValue, decimal PreviousAssessedValue, decimal NewMarketValue, decimal NewAssessedValue,
    IReadOnlyList<GeneralRevisionSuspensionDto> Suspensions, bool Suspended, IReadOnlyList<GeneralRevisionRunDto> Runs, bool RunActive,
    DateTimeOffset CreatedAt, DateTimeOffset? CompletedAt, string? CancellationReason);

public sealed record GeneralRevisionScopeDto(Guid MunicipalityId, string MunicipalityName);

public sealed record GeneralRevisionSummaryDto(
    Guid Id, int RevisionYear, DateOnly EffectiveDate, string SmvReference, GeneralRevisionStatus Status, string Scope, int ItemCount, DateTimeOffset CreatedAt);

public sealed class GeneralRevisionItemSearchRequest : PagedRequest
{
    public Guid? BarangayId { get; set; }
    public GeneralRevisionItemStatus? Status { get; set; }
    /// <summary>The item's current assessment's workflow status.</summary>
    public WorkflowStatus? AssessmentStatus { get; set; }
    /// <summary>PIN or unit number.</summary>
    public string? Search { get; set; }
    /// <summary>The Tax Declaration declaring the item's assessment, by workflow status.</summary>
    public WorkflowStatus? TaxDeclarationStatus { get; set; }
    public Guid? InspectorId { get; set; }
    public GeneralRevisionInspectionFilter? Inspection { get; set; }
    public Guid? ItemId { get; set; }
}

public enum GeneralRevisionInspectionFilter
{
    Unassigned = 0,
    /// <summary>Assigned and not yet inspected.</summary>
    Awaiting = 1,
    Inspected = 2,
}

public sealed record GeneralRevisionItemDto(
    Guid Id, Guid RpuId, Guid PropertyId, string Pin, string RpuNumber, RpuType RpuType, Guid BarangayId, string BarangayName,
    Guid? PreviousAssessmentId, decimal? PreviousMarketValue, decimal? PreviousAssessedValue, GeneralRevisionItemStatus Status, string? FailureReason,
    Guid? AssessmentId, WorkflowStatus? AssessmentStatus, decimal? NewMarketValue, decimal? NewAssessedValue, decimal? AssessedValueChange,
    DateTimeOffset? ProcessedAt, Guid? TaxDeclarationId, string? TaxDeclarationNumber, WorkflowStatus? TaxDeclarationStatus,
    Guid? InspectorId, string? InspectorName, string? InspectionRoute, DateOnly? InspectedOn, string? InspectionNotes, bool? InspectionFoundChanges,
    string? ExclusionReason);

public interface IGeneralRevisionProgrammeService
{
    Task<Result<GeneralRevisionDto>> CreateAsync(CreateGeneralRevisionRequest request, CancellationToken cancellationToken = default);
    Task<Result<GeneralRevisionDto>> UpdateReferencesAsync(Guid id, UpdateGeneralRevisionReferencesRequest request, CancellationToken cancellationToken = default);
    Task<Result<GeneralRevisionDto>> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<GeneralRevisionSummaryDto>>> ListAsync(CancellationToken cancellationToken = default);
    Task<Result<PagedResult<GeneralRevisionItemDto>>> SearchItemsAsync(Guid id, GeneralRevisionItemSearchRequest request, CancellationToken cancellationToken = default);
    /// <summary>Queues a compile or value run (a background job); refused while suspended or while another run is active.</summary>
    Task<Result<GeneralRevisionRunDto>> StartRunAsync(Guid id, StartGeneralRevisionRunRequest request, CancellationToken cancellationToken = default);
    Task<Result<GeneralRevisionDto>> SuspendAsync(Guid id, SuspendGeneralRevisionRequest request, CancellationToken cancellationToken = default);
    Task<Result<GeneralRevisionDto>> LiftSuspensionAsync(Guid id, Guid suspensionId, LiftGeneralRevisionSuspensionRequest request, CancellationToken cancellationToken = default);
    /// <summary>Only while no item's assessment has gone past Draft; the drafts are cancelled with it.</summary>
    Task<Result<GeneralRevisionDto>> CancelAsync(Guid id, CancelGeneralRevisionRequest request, CancellationToken cancellationToken = default);
    /// <summary>Assigns the chosen items to an appraiser and route for field review (GRI 9–10); returns how many were assigned.</summary>
    Task<Result<int>> AssignInspectionAsync(Guid id, AssignGeneralRevisionInspectionRequest request, CancellationToken cancellationToken = default);
    Task<Result<GeneralRevisionItemDto>> RecordInspectionAsync(Guid id, Guid itemId, RecordGeneralRevisionInspectionRequest request, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<GeneralRevisionRunIssueDto>>> ListRunIssuesAsync(Guid id, Guid runId, CancellationToken cancellationToken = default);
}

/// <summary>
/// General revision programmes (docs/analysis/smv-preparation-general-revision.md §4.6): a revision year and effectivity,
/// the certified SMV it applies, its scope, suspensions, and the runs that compile its units and value them. Large work
/// runs as background jobs (CLAUDE.md §72); this service only records and queues.
/// </summary>
public sealed class GeneralRevisionProgrammeService(
    IApplicationDbContext db, IClock clock, ICurrentUserService currentUser, IJurisdiction jurisdiction, IBackgroundJobScheduler scheduler,
    IOptions<GeneralRevisionOptions> options) : IGeneralRevisionProgrammeService
{
    public async Task<Result<GeneralRevisionDto>> CreateAsync(CreateGeneralRevisionRequest r, CancellationToken cancellationToken = default)
    {
        var municipalities = (r.MunicipalityIds ?? []).Distinct().ToList();
        if (r.RevisionYear is < 1990 or > 2200 || r.EffectiveDate == default || municipalities.Count == 0
            || r.OfficeOrderReference?.Length > 200 || r.OrdinanceReference?.Length > 200 || r.Description?.Length > 1000)
        {
            return Fail("VALIDATION_FAILED", "A revision year, an effective date, the SMV and at least one city/municipality are required; a text field is too long.");
        }
        if (await db.Municipalities.CountAsync(x => municipalities.Contains(x.Id), cancellationToken) != municipalities.Count)
        {
            return Fail("MUNICIPALITY_NOT_FOUND", "A city/municipality in the scope does not exist.");
        }
        if (municipalities.Any(m => !jurisdiction.Allows(m)))
        {
            return Fail(JurisdictionErrors.Code, JurisdictionErrors.Message);
        }
        var smv = await db.Smvs.AsNoTracking().Include(x => x.Coverage).FirstOrDefaultAsync(x => x.Id == r.SmvId, cancellationToken);
        if (smv is null)
        {
            return Fail("SMV_NOT_FOUND", "The specified SMV does not exist.");
        }
        // GRI 2: the approved (certified) SMV is applied; it must be in force at the revision's effectivity and cover the scope.
        if (smv.Basis == SmvBasis.Amendment)
        {
            return Fail("SMV_NOT_APPLICABLE", "A general revision applies the SMV itself; its amendments in force apply through it.");
        }
        if (smv.Status != WorkflowStatus.Approved || smv.EffectivityDate > r.EffectiveDate)
        {
            return Fail("SMV_NOT_APPLICABLE", "The SMV must be approved in PRIME and in force on the revision's effective date.");
        }
        if (smv.Coverage.Count > 0 && municipalities.Any(m => smv.Coverage.All(c => c.MunicipalityId != m)))
        {
            return Fail("SMV_NOT_APPLICABLE", "The SMV does not cover every city/municipality in the scope.");
        }
        var overlapping = await db.GeneralRevisionScopes.AsNoTracking()
            .Where(s => municipalities.Contains(s.MunicipalityId))
            .Join(db.GeneralRevisionProgrammes, s => s.GeneralRevisionProgrammeId, p => p.Id, (s, p) => p)
            .AnyAsync(p => p.RevisionYear == r.RevisionYear && p.Status != GeneralRevisionStatus.Cancelled, cancellationToken);
        if (overlapping)
        {
            return Fail("GENERAL_REVISION_DUPLICATE", $"A {r.RevisionYear} general revision already covers one of these cities/municipalities.");
        }
        var programme = new GeneralRevisionProgramme
        {
            RevisionYear = r.RevisionYear, EffectiveDate = r.EffectiveDate, SmvId = r.SmvId,
            OfficeOrderReference = Clean(r.OfficeOrderReference), OrdinanceReference = Clean(r.OrdinanceReference), Description = Clean(r.Description),
            Scope = municipalities.Select(m => new GeneralRevisionScope { MunicipalityId = m }).ToList(),
        };
        db.GeneralRevisionProgrammes.Add(programme);
        await db.SaveChangesAsync(cancellationToken);
        return await GetAsync(programme.Id, cancellationToken);
    }

    public async Task<Result<GeneralRevisionDto>> UpdateReferencesAsync(Guid id, UpdateGeneralRevisionReferencesRequest r, CancellationToken cancellationToken = default)
    {
        if (r.OfficeOrderReference?.Length > 200 || r.OrdinanceReference?.Length > 200 || r.Description?.Length > 1000)
        {
            return Fail("VALIDATION_FAILED", "References max 200; description max 1000.");
        }
        var p = await OpenAsync(id, cancellationToken);
        if (p.IsFailure)
        {
            return Fail(p.Code!, p.Message!);
        }
        (p.Value.OfficeOrderReference, p.Value.OrdinanceReference, p.Value.Description) = (Clean(r.OfficeOrderReference), Clean(r.OrdinanceReference), Clean(r.Description));
        await db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    public async Task<Result<GeneralRevisionRunDto>> StartRunAsync(Guid id, StartGeneralRevisionRunRequest r, CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(r.Mode) || (r.Mode == GeneralRevisionRunMode.Compile && (r.ItemIds is { Count: > 0 } || r.BarangayId is not null)))
        {
            return Result.Failure<GeneralRevisionRunDto>("VALIDATION_FAILED", "Unknown run mode; a compile run takes the whole scope.");
        }
        var reason = Clean(r.Reason);
        if (r.Mode == GeneralRevisionRunMode.Reject ? reason is null || reason.Length > 1000 : reason is not null)
        {
            return Result.Failure<GeneralRevisionRunDto>("VALIDATION_FAILED", "A reject run needs a reason (max 1000); other runs take none.");
        }
        var opened = await OpenAsync(id, cancellationToken);
        if (opened.IsFailure)
        {
            return Result.Failure<GeneralRevisionRunDto>(opened.Code!, opened.Message!);
        }
        var p = opened.Value;
        if (InForce(p.Suspensions, clock.Today) is { } suspension)
        {
            return Result.Failure<GeneralRevisionRunDto>("GENERAL_REVISION_SUSPENDED",
                $"The revision is suspended ({suspension.Kind}, {suspension.Reference}){(suspension.UntilDate is { } until ? $" until {until:yyyy-MM-dd}" : " until lifted")}.");
        }
        if (await db.GeneralRevisionJobs.AnyAsync(x => x.GeneralRevisionProgrammeId == id
                && (x.Status == JobExecutionStatus.Queued || x.Status == JobExecutionStatus.Running), cancellationToken))
        {
            return Result.Failure<GeneralRevisionRunDto>("GENERAL_REVISION_RUN_ACTIVE", "Another run of this revision is queued or running; wait for it to finish.");
        }
        List<Guid> itemIds = [];
        if (r.Mode == GeneralRevisionRunMode.Value)
        {
            var eligible = db.GeneralRevisionItems.Where(x => x.GeneralRevisionProgrammeId == id);
            if (r.ItemIds is { Count: > 0 } chosen)
            {
                var ids = chosen.Distinct().ToList();
                var found = await eligible.Where(x => ids.Contains(x.Id))
                    .Select(x => new { x.Id, x.Status, AssessmentStatus = x.Assessment != null ? (WorkflowStatus?)x.Assessment.Status : null }).ToListAsync(cancellationToken);
                if (found.Count != ids.Count)
                {
                    return Result.Failure<GeneralRevisionRunDto>("GENERAL_REVISION_ITEM_NOT_FOUND", "An item is not part of this revision.");
                }
                if (found.Any(x => x.Status == GeneralRevisionItemStatus.Excluded))
                {
                    return Result.Failure<GeneralRevisionRunDto>("GENERAL_REVISION_ITEM_EXCLUDED", "An excluded unit is not valued; include it again first.");
                }
                if (found.FirstOrDefault(x => x.AssessmentStatus is { } s && s != WorkflowStatus.Draft && s != WorkflowStatus.Rejected && s != WorkflowStatus.Cancelled) is { } busy)
                {
                    return Result.Failure<GeneralRevisionRunDto>("GENERAL_REVISION_ITEM_IN_REVIEW",
                        $"An item's assessment is {busy.AssessmentStatus}; only items whose assessment is still a draft (or was rejected) can be valued again.");
                }
                itemIds = ids;
            }
            else
            {
                itemIds = await eligible.Where(x => x.Status == GeneralRevisionItemStatus.Pending || (r.IncludeFailed && x.Status == GeneralRevisionItemStatus.Failed))
                    .Where(x => r.BarangayId == null || x.BarangayId == r.BarangayId)
                    .OrderBy(x => x.Pin).ThenBy(x => x.RpuNumber).Select(x => x.Id).ToListAsync(cancellationToken);
            }
            if (itemIds.Count == 0)
            {
                return Result.Failure<GeneralRevisionRunDto>("GENERAL_REVISION_NOTHING_TO_RUN", "No item waits for valuation; compile the scope first, or choose items to value again.");
            }
        }
        else if (r.Mode != GeneralRevisionRunMode.Compile)
        {
            // A batch action takes the items in the state it acts on, in PIN order (GRI 15); each item still goes through the
            // ordinary single-record action, so the approval chain and maker-checker hold per item (Q14).
            var actionable = Actionable(db, db.GeneralRevisionItems.Where(x => x.GeneralRevisionProgrammeId == id), r.Mode);
            if (r.ItemIds is { Count: > 0 } chosen)
            {
                var ids = chosen.Distinct().ToList();
                actionable = actionable.Where(x => ids.Contains(x.Id));
            }
            if (r.BarangayId is { } barangay)
            {
                actionable = actionable.Where(x => x.BarangayId == barangay);
            }
            itemIds = await actionable.OrderBy(x => x.Pin).ThenBy(x => x.RpuNumber).Select(x => x.Id).ToListAsync(cancellationToken);
            if (itemIds.Count == 0)
            {
                return Result.Failure<GeneralRevisionRunDto>("GENERAL_REVISION_NOTHING_TO_RUN", $"No item {Awaiting(r.Mode)}.");
            }
        }
        var job = new GeneralRevisionJob
        {
            GeneralRevisionProgrammeId = id, Mode = r.Mode, RevisionYear = p.RevisionYear, EffectiveDate = p.EffectiveDate,
            Status = JobExecutionStatus.Queued, TotalCount = itemIds.Count, StartedBy = currentUser.AppUserId, StartedAt = clock.UtcNow, Reason = reason,
        };
        db.GeneralRevisionJobs.Add(job);
        if (p.Status == GeneralRevisionStatus.Planned)
        {
            p.Status = GeneralRevisionStatus.InProgress;
        }
        await db.SaveChangesAsync(cancellationToken);
        scheduler.Enqueue<GeneralRevisionProgrammeRunner>(runner => runner.RunAsync(job.Id, itemIds, CancellationToken.None));
        return Result.Success(ToDto(job));
    }

    public async Task<Result<GeneralRevisionDto>> SuspendAsync(Guid id, SuspendGeneralRevisionRequest r, CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(r.Kind) || r.FromDate == default || string.IsNullOrWhiteSpace(r.Reference) || r.Reference.Length > 300 || r.Remarks?.Length > 1000)
        {
            return Fail("VALIDATION_FAILED", "The kind, the first day and the reference of the declaration are required (max 300); remarks max 1000.");
        }
        var opened = await OpenAsync(id, cancellationToken);
        if (opened.IsFailure)
        {
            return Fail(opened.Code!, opened.Message!);
        }
        var p = opened.Value;
        var days = options.Value.CalamitySuspensionDays;
        var suspension = new GeneralRevisionSuspension
        {
            GeneralRevisionProgrammeId = id, Kind = r.Kind, Reference = r.Reference.Trim(), Remarks = Clean(r.Remarks), FromDate = r.FromDate,
        };
        switch (r.Kind)
        {
            case GeneralRevisionSuspensionKind.LocalCalamity:
                suspension.UntilDate = r.FromDate.AddDays(days - 1);
                break;
            case GeneralRevisionSuspensionKind.Extension:
                // An extension follows a calamity suspension (or an earlier extension) still in force, for another period.
                var last = p.Suspensions.Where(s => s.Kind != GeneralRevisionSuspensionKind.NationalEmergency && s.LiftedOn == null && s.UntilDate is not null)
                    .MaxBy(s => s.UntilDate);
                if (last is null || last.UntilDate < clock.Today.AddDays(-1))
                {
                    return Fail("GENERAL_REVISION_NO_SUSPENSION", "An extension follows a calamity suspension that has not ended.");
                }
                suspension.FromDate = last.UntilDate!.Value.AddDays(1);
                suspension.UntilDate = suspension.FromDate.AddDays(days - 1);
                break;
            case GeneralRevisionSuspensionKind.NationalEmergency:
                suspension.UntilDate = null;
                break;
        }
        db.GeneralRevisionSuspensions.Add(suspension);
        await db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    public async Task<Result<GeneralRevisionDto>> LiftSuspensionAsync(Guid id, Guid suspensionId, LiftGeneralRevisionSuspensionRequest r, CancellationToken cancellationToken = default)
    {
        var s = await db.GeneralRevisionSuspensions.FirstOrDefaultAsync(x => x.Id == suspensionId && x.GeneralRevisionProgrammeId == id, cancellationToken);
        if (s is null)
        {
            return Fail("GENERAL_REVISION_SUSPENSION_NOT_FOUND", "No such suspension of this revision.");
        }
        if (s.LiftedOn is not null || r.LiftedOn < s.FromDate || r.LiftedOn > clock.Today)
        {
            return Fail("VALIDATION_FAILED", "A suspension is lifted once, on or after its first day and not after today.");
        }
        s.LiftedOn = r.LiftedOn;
        s.UntilDate = s.UntilDate is { } until && until < r.LiftedOn ? until : r.LiftedOn;
        await db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    public async Task<Result<GeneralRevisionDto>> CancelAsync(Guid id, CancelGeneralRevisionRequest r, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(r.Reason) || r.Reason.Length > 1000)
        {
            return Fail("VALIDATION_FAILED", "A reason is required (max 1000).");
        }
        var opened = await OpenAsync(id, cancellationToken);
        if (opened.IsFailure)
        {
            return Fail(opened.Code!, opened.Message!);
        }
        if (await db.GeneralRevisionJobs.AnyAsync(x => x.GeneralRevisionProgrammeId == id
                && (x.Status == JobExecutionStatus.Queued || x.Status == JobExecutionStatus.Running), cancellationToken))
        {
            return Fail("GENERAL_REVISION_RUN_ACTIVE", "A run is queued or running.");
        }
        var drafts = await db.GeneralRevisionItems.IgnoreQueryFilters().Where(x => x.GeneralRevisionProgrammeId == id && x.AssessmentId != null)
            .Select(x => x.Assessment!).ToListAsync(cancellationToken);
        if (drafts.Any(a => a.Status is not (WorkflowStatus.Draft or WorkflowStatus.Rejected or WorkflowStatus.Cancelled)))
        {
            return Fail("GENERAL_REVISION_IN_REVIEW", "Some of its assessments are in review or beyond; a revision is cancelled only before that.");
        }
        foreach (var a in drafts.Where(a => a.Status == WorkflowStatus.Draft))
        {
            a.Status = WorkflowStatus.Cancelled;
        }
        (opened.Value.Status, opened.Value.CancellationReason) = (GeneralRevisionStatus.Cancelled, r.Reason.Trim());
        currentUser.Reason = r.Reason.Trim();
        await db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    public async Task<Result<int>> AssignInspectionAsync(Guid id, AssignGeneralRevisionInspectionRequest r, CancellationToken cancellationToken = default)
    {
        var ids = (r.ItemIds ?? []).Distinct().ToList();
        var route = Clean(r.Route);
        if (ids.Count == 0 || route?.Length > 100 || (r.InspectorId is null && route is not null))
        {
            return Result.Failure<int>("VALIDATION_FAILED", "Choose the items; a route (max 100) goes with an inspector.");
        }
        var opened = await OpenAsync(id, cancellationToken);
        if (opened.IsFailure)
        {
            return Result.Failure<int>(opened.Code!, opened.Message!);
        }
        if (r.InspectorId is { } inspector && !await db.AppUsers.AnyAsync(u => u.Id == inspector, cancellationToken))
        {
            return Result.Failure<int>("USER_NOT_FOUND", "The inspector is not a user of PRIME.");
        }
        var items = await db.GeneralRevisionItems.Where(x => x.GeneralRevisionProgrammeId == id && ids.Contains(x.Id)).ToListAsync(cancellationToken);
        if (items.Count != ids.Count)
        {
            return Result.Failure<int>("GENERAL_REVISION_ITEM_NOT_FOUND", "An item is not part of this revision.");
        }
        foreach (var item in items)
        {
            (item.InspectorId, item.InspectionRoute) = (r.InspectorId, route);
        }
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success(items.Count);
    }

    public async Task<Result<GeneralRevisionItemDto>> RecordInspectionAsync(Guid id, Guid itemId, RecordGeneralRevisionInspectionRequest r,
        CancellationToken cancellationToken = default)
    {
        if (r.InspectedOn == default || r.InspectedOn > clock.Today || r.Notes?.Length > 2000)
        {
            return Result.Failure<GeneralRevisionItemDto>("VALIDATION_FAILED", "The inspection date is required and not in the future; notes max 2000.");
        }
        var opened = await OpenAsync(id, cancellationToken);
        if (opened.IsFailure)
        {
            return Result.Failure<GeneralRevisionItemDto>(opened.Code!, opened.Message!);
        }
        var item = await db.GeneralRevisionItems.Include(x => x.Assessment).FirstOrDefaultAsync(x => x.Id == itemId && x.GeneralRevisionProgrammeId == id, cancellationToken);
        if (item is null)
        {
            return Result.Failure<GeneralRevisionItemDto>("GENERAL_REVISION_ITEM_NOT_FOUND", "An item is not part of this revision.");
        }
        if (r.FoundChanges)
        {
            // The unit is valued again from what the inspection corrected; an assessment already in review must be returned first.
            if (item.Assessment is { Status: not (WorkflowStatus.Draft or WorkflowStatus.Rejected or WorkflowStatus.Cancelled) } busy)
            {
                return Result.Failure<GeneralRevisionItemDto>("GENERAL_REVISION_ITEM_IN_REVIEW",
                    $"Its assessment is {busy.Status}; reject it before recording changes that need a new valuation.");
            }
            item.Status = GeneralRevisionItemStatus.Pending;
            item.FailureReason = null;
        }
        (item.InspectedOn, item.InspectionNotes, item.InspectionFoundChanges, item.InspectionRecordedBy) =
            (r.InspectedOn, Clean(r.Notes), r.FoundChanges, currentUser.AppUserId);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success(await ItemDtos(db.GeneralRevisionItems.AsNoTracking().Where(x => x.Id == itemId)).FirstAsync(cancellationToken));
    }

    public async Task<Result<IReadOnlyList<GeneralRevisionRunIssueDto>>> ListRunIssuesAsync(Guid id, Guid runId, CancellationToken cancellationToken = default)
    {
        if (!await db.GeneralRevisionJobs.AnyAsync(x => x.Id == runId && x.GeneralRevisionProgrammeId == id, cancellationToken))
        {
            return Result.Failure<IReadOnlyList<GeneralRevisionRunIssueDto>>("GENERAL_REVISION_RUN_NOT_FOUND", "No such run of this revision.");
        }
        var issues = await db.GeneralRevisionRunIssues.AsNoTracking().Where(x => x.GeneralRevisionJobId == runId)
            .OrderBy(x => x.Pin).ThenBy(x => x.RpuNumber).Take(1000)
            .Select(x => new GeneralRevisionRunIssueDto(x.GeneralRevisionItemId, x.Pin, x.RpuNumber, x.Code, x.Message, x.Failed)).ToListAsync(cancellationToken);
        return Result.Success<IReadOnlyList<GeneralRevisionRunIssueDto>>(issues);
    }

    /// <summary>The Tax Declaration declaring an item's assessment: the latest one still alive (draft, in review or approved).</summary>
    internal static IQueryable<TaxDeclaration> Declaring(IApplicationDbContext db, Guid? assessmentId) =>
        db.TaxDeclarations.Where(t => t.AssessmentId == assessmentId
                && (t.Status == WorkflowStatus.Draft || t.Status == WorkflowStatus.PendingReview || t.Status == WorkflowStatus.Approved))
            .OrderByDescending(t => t.CreatedAt);

    /// <summary>The items a batch action works on: those whose assessment, or the Tax Declaration declaring it, is in the state it acts on.</summary>
    internal static IQueryable<GeneralRevisionItem> Actionable(IApplicationDbContext db, IQueryable<GeneralRevisionItem> items, GeneralRevisionRunMode mode) => mode switch
    {
        GeneralRevisionRunMode.Submit => items.Where(x => x.Assessment != null && x.Assessment.Status == WorkflowStatus.Draft),
        GeneralRevisionRunMode.Approve or GeneralRevisionRunMode.Reject => items.Where(x => x.Assessment != null && x.Assessment.Status == WorkflowStatus.PendingReview),
        GeneralRevisionRunMode.Post => items.Where(x => x.Assessment != null && x.Assessment.Status == WorkflowStatus.Approved),
        GeneralRevisionRunMode.SubmitTaxDeclarations => items.Where(x => x.AssessmentId != null
            && db.TaxDeclarations.Any(t => t.AssessmentId == x.AssessmentId && t.Status == WorkflowStatus.Draft && t.PropertyTransactionId == null)),
        GeneralRevisionRunMode.ApproveTaxDeclarations => items.Where(x => x.AssessmentId != null
            && db.TaxDeclarations.Any(t => t.AssessmentId == x.AssessmentId && t.Status == WorkflowStatus.PendingReview && t.PropertyTransactionId == null)),
        // LGC §223: a first assessment, or a changed assessed value, needs notice; one not yet listed on a notice (other than a cancelled one).
        GeneralRevisionRunMode.GenerateNotices => items.Where(x => x.Assessment != null && x.Assessment.Status == WorkflowStatus.Posted
            && (x.PreviousAssessmentId == null || x.PreviousAssessedValue != x.NewAssessedValue)
            && !db.NoticeOfAssessmentItems.Any(i => i.AssessmentId == x.AssessmentId
                && db.NoticesOfAssessment.Any(n => n.Id == i.NoticeOfAssessmentId && n.Status != NoticeStatus.Cancelled))),
        GeneralRevisionRunMode.IssueNotices => items.Where(x => x.AssessmentId != null && db.NoticeOfAssessmentItems.Any(i => i.AssessmentId == x.AssessmentId
            && db.NoticesOfAssessment.Any(n => n.Id == i.NoticeOfAssessmentId && n.Status == NoticeStatus.Draft))),
        _ => items.Where(_ => false),
    };

    private static string Awaiting(GeneralRevisionRunMode mode) => mode switch
    {
        GeneralRevisionRunMode.Submit => "has a draft assessment to submit",
        GeneralRevisionRunMode.Approve or GeneralRevisionRunMode.Reject => "has an assessment pending review",
        GeneralRevisionRunMode.Post => "has an approved assessment waiting to be posted",
        GeneralRevisionRunMode.SubmitTaxDeclarations => "has a draft Tax Declaration to submit; post the approved assessments first",
        GeneralRevisionRunMode.ApproveTaxDeclarations => "has a Tax Declaration pending review",
        GeneralRevisionRunMode.GenerateNotices => "needs a notice of assessment it does not have (a posted unit whose assessed value changed or that was first assessed)",
        GeneralRevisionRunMode.IssueNotices => "is on a draft notice of assessment",
        _ => "waits for this action",
    };

    public async Task<Result<GeneralRevisionDto>> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var ct = cancellationToken;
        var p = await db.GeneralRevisionProgrammes.AsNoTracking().Include(x => x.Smv).Include(x => x.Scope).ThenInclude(s => s.Municipality)
            .Include(x => x.Suspensions).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (p is null)
        {
            return NotFound();
        }
        var items = db.GeneralRevisionItems.AsNoTracking().Where(x => x.GeneralRevisionProgrammeId == id);
        var byStatus = (await items.GroupBy(x => x.Status).Select(g => new { g.Key, Count = g.Count() }).ToListAsync(ct))
            .ToDictionary(x => x.Key.ToString(), x => x.Count);
        var byAssessment = (await items.Where(x => x.AssessmentId != null).GroupBy(x => x.Assessment!.Status).Select(g => new { g.Key, Count = g.Count() }).ToListAsync(ct))
            .ToDictionary(x => x.Key.ToString(), x => x.Count);
        var totals = await items.GroupBy(_ => 1).Select(g => new
        {
            Count = g.Count(),
            PrevMv = g.Sum(x => x.PreviousMarketValue ?? 0), PrevAv = g.Sum(x => x.PreviousAssessedValue ?? 0),
            NewMv = g.Sum(x => x.NewMarketValue ?? 0), NewAv = g.Sum(x => x.NewAssessedValue ?? 0),
        }).FirstOrDefaultAsync(ct);
        var runs = await db.GeneralRevisionJobs.AsNoTracking().Where(x => x.GeneralRevisionProgrammeId == id).OrderByDescending(x => x.StartedAt).Take(10).ToListAsync(ct);
        var runIds = runs.Select(x => x.Id).ToList();
        var issueCounts = await db.GeneralRevisionRunIssues.AsNoTracking().Where(x => runIds.Contains(x.GeneralRevisionJobId))
            .GroupBy(x => x.GeneralRevisionJobId).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, ct);
        var today = clock.Today;
        return Result.Success(new GeneralRevisionDto(
            p.Id, p.RevisionYear, p.EffectiveDate, p.SmvId, p.Smv!.Reference, p.OfficeOrderReference, p.OrdinanceReference, p.Description, p.Status,
            p.Scope.OrderBy(s => s.Municipality!.Name).Select(s => new GeneralRevisionScopeDto(s.MunicipalityId, s.Municipality!.Name)).ToList(),
            totals?.Count ?? 0, byStatus, byAssessment, totals?.PrevMv ?? 0, totals?.PrevAv ?? 0, totals?.NewMv ?? 0, totals?.NewAv ?? 0,
            p.Suspensions.OrderByDescending(s => s.FromDate).Select(s => new GeneralRevisionSuspensionDto(s.Id, s.Kind, s.FromDate, s.UntilDate, s.Reference, s.Remarks,
                s.LiftedOn, IsInForce(s, today))).ToList(),
            InForce(p.Suspensions, today) is not null, runs.Select(x => ToDto(x) with { IssueCount = issueCounts.GetValueOrDefault(x.Id) }).ToList(),
            runs.Any(x => x.Status is JobExecutionStatus.Queued or JobExecutionStatus.Running), p.CreatedAt, p.CompletedAt, p.CancellationReason));
    }

    public async Task<Result<IReadOnlyList<GeneralRevisionSummaryDto>>> ListAsync(CancellationToken cancellationToken = default)
    {
        var rows = await db.GeneralRevisionProgrammes.AsNoTracking().OrderByDescending(x => x.RevisionYear).ThenByDescending(x => x.CreatedAt).Take(100)
            .Select(x => new
            {
                x.Id, x.RevisionYear, x.EffectiveDate, Smv = x.Smv!.OrdinanceNumber ?? x.Smv.CertificationReference ?? "", x.Status, x.CreatedAt,
                Scope = x.Scope.Select(s => s.Municipality!.Name).ToList(),
                Items = db.GeneralRevisionItems.Count(i => i.GeneralRevisionProgrammeId == x.Id),
            }).ToListAsync(cancellationToken);
        return Result.Success<IReadOnlyList<GeneralRevisionSummaryDto>>(rows.Select(x => new GeneralRevisionSummaryDto(
            x.Id, x.RevisionYear, x.EffectiveDate, x.Smv, x.Status, string.Join(", ", x.Scope.Order()), x.Items, x.CreatedAt)).ToList());
    }

    public async Task<Result<PagedResult<GeneralRevisionItemDto>>> SearchItemsAsync(Guid id, GeneralRevisionItemSearchRequest r, CancellationToken cancellationToken = default)
    {
        var query = db.GeneralRevisionItems.AsNoTracking().Where(x => x.GeneralRevisionProgrammeId == id);
        if (r.BarangayId is { } b) query = query.Where(x => x.BarangayId == b);
        if (r.Status is { } s) query = query.Where(x => x.Status == s);
        if (r.AssessmentStatus is { } a) query = query.Where(x => x.Assessment != null && x.Assessment.Status == a);
        if (!string.IsNullOrWhiteSpace(r.Search))
        {
            var term = r.Search.Trim().ToLower();
            query = query.Where(x => x.Pin.ToLower().Contains(term) || x.RpuNumber.ToLower().Contains(term));
        }
        if (r.TaxDeclarationStatus is { } t)
        {
            query = query.Where(x => x.AssessmentId != null && db.TaxDeclarations.Any(d => d.AssessmentId == x.AssessmentId && d.Status == t));
        }
        if (r.InspectorId is { } inspector) query = query.Where(x => x.InspectorId == inspector);
        if (r.ItemId is { } itemId) query = query.Where(x => x.Id == itemId);
        query = r.Inspection switch
        {
            GeneralRevisionInspectionFilter.Unassigned => query.Where(x => x.InspectorId == null && x.InspectedOn == null),
            GeneralRevisionInspectionFilter.Awaiting => query.Where(x => x.InspectorId != null && x.InspectedOn == null),
            GeneralRevisionInspectionFilter.Inspected => query.Where(x => x.InspectedOn != null),
            _ => query,
        };
        var total = await query.CountAsync(cancellationToken);
        var items = await ItemDtos(query.OrderBy(x => x.Pin).ThenBy(x => x.RpuNumber).Skip((r.Page - 1) * r.PageSize).Take(r.PageSize))
            .ToListAsync(cancellationToken);
        return Result.Success(new PagedResult<GeneralRevisionItemDto> { Items = items, TotalCount = total, Page = r.Page, PageSize = r.PageSize });
    }

    private IQueryable<GeneralRevisionItemDto> ItemDtos(IQueryable<GeneralRevisionItem> query) =>
        query.Select(x => new
        {
            Item = x,
            AssessmentStatus = x.Assessment != null ? (WorkflowStatus?)x.Assessment.Status : null,
            Barangay = db.Barangays.Where(g => g.Id == x.BarangayId).Select(g => g.Name).FirstOrDefault(),
            Inspector = db.AppUsers.Where(u => u.Id == x.InspectorId).Select(u => u.DisplayName).FirstOrDefault(),
            Td = db.TaxDeclarations.Where(t => x.AssessmentId != null && t.AssessmentId == x.AssessmentId
                    && (t.Status == WorkflowStatus.Draft || t.Status == WorkflowStatus.PendingReview || t.Status == WorkflowStatus.Approved))
                .OrderByDescending(t => t.CreatedAt).Select(t => new { t.Id, t.TaxDeclarationNumber, t.Status }).FirstOrDefault(),
        }).Select(r => new GeneralRevisionItemDto(
            r.Item.Id, r.Item.RpuId, r.Item.PropertyId, r.Item.Pin, r.Item.RpuNumber, r.Item.RpuType, r.Item.BarangayId, r.Barangay ?? "",
            r.Item.PreviousAssessmentId, r.Item.PreviousMarketValue, r.Item.PreviousAssessedValue, r.Item.Status, r.Item.FailureReason,
            r.Item.AssessmentId, r.AssessmentStatus, r.Item.NewMarketValue, r.Item.NewAssessedValue,
            r.Item.NewAssessedValue - r.Item.PreviousAssessedValue, r.Item.ProcessedAt,
            r.Td != null ? r.Td.Id : null, r.Td != null ? r.Td.TaxDeclarationNumber : null, r.Td != null ? r.Td.Status : null,
            r.Item.InspectorId, r.Inspector, r.Item.InspectionRoute, r.Item.InspectedOn, r.Item.InspectionNotes, r.Item.InspectionFoundChanges,
            r.Item.ExclusionReason));

    /// <summary>The programme, tracked, if it is open (not completed or cancelled) and every city/municipality it covers is in the user's jurisdiction.</summary>
    private async Task<Result<GeneralRevisionProgramme>> OpenAsync(Guid id, CancellationToken ct)
    {
        var p = await db.GeneralRevisionProgrammes.Include(x => x.Scope).Include(x => x.Suspensions).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (p is null)
        {
            return Result.Failure<GeneralRevisionProgramme>("GENERAL_REVISION_NOT_FOUND", "No general revision was found with the given id.");
        }
        if (p.Scope.Any(s => !jurisdiction.Allows(s.MunicipalityId)))
        {
            return Result.Failure<GeneralRevisionProgramme>(JurisdictionErrors.Code, "This revision covers a city/municipality outside your office's jurisdiction.");
        }
        if (p.Status is GeneralRevisionStatus.Completed or GeneralRevisionStatus.Cancelled)
        {
            return Result.Failure<GeneralRevisionProgramme>("GENERAL_REVISION_CLOSED", $"The revision is {p.Status}.");
        }
        return Result.Success(p);
    }

    private static bool IsInForce(GeneralRevisionSuspension s, DateOnly today) =>
        s.FromDate <= today && s.LiftedOn is null && (s.UntilDate is null || s.UntilDate >= today);

    private static GeneralRevisionSuspension? InForce(IEnumerable<GeneralRevisionSuspension> suspensions, DateOnly today) =>
        suspensions.FirstOrDefault(s => IsInForce(s, today));

    private static GeneralRevisionRunDto ToDto(GeneralRevisionJob x) =>
        new(x.Id, x.Mode, x.Status, x.TotalCount, x.ProcessedCount, x.FailedCount, x.StartedAt, x.CompletedAt, x.Remarks, x.Reason);

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static Result<GeneralRevisionDto> Fail(string code, string message) => Result.Failure<GeneralRevisionDto>(code, message);

    private static Result<GeneralRevisionDto> NotFound() => Fail("GENERAL_REVISION_NOT_FOUND", "No general revision was found with the given id.");
}
