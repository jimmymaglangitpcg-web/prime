using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Prime.Application.Common;
using Prime.Application.Common.Interfaces;
using Prime.Application.Features.Notices;
using Prime.Application.Features.Registers;
using Prime.Domain.Entities;
using Prime.Domain.Entities.Notices;
using Prime.Domain.Enums;

namespace Prime.Application.Features.GeneralRevision;

public sealed class GeneralRevisionNoticeSearchRequest : PagedRequest
{
    public NoticeStatus? Status { get; set; }
    /// <summary>Notice number or addressee.</summary>
    public string? Search { get; set; }
}

/// <param name="IssueOverdue">Still a draft after the LGC §223 period from approval.</param>
public sealed record GeneralRevisionNoticeDto(
    Guid Id, string? NoticeNumber, string AddresseeNames, int ItemCount, string FirstPin, decimal AssessedValue, NoticeStatus Status,
    DateOnly IssueDueDate, bool IssueOverdue, DateTimeOffset? IssuedAt, NoticeServiceMode? ServiceMode, DateOnly? ReceivedDate, DateOnly? AppealDeadline);

/// <param name="ServedTo">Who received it; omitted, the addressee named on each notice.</param>
public sealed record RecordGeneralRevisionNoticeServiceRequest(
    IReadOnlyList<Guid> NoticeIds, NoticeServiceMode ServiceMode, DateOnly ReceivedDate, string ProofReference, string? ServedTo, string? Notes,
    DateOnly? SentDate = null);

public sealed record NoticeServiceFailureDto(Guid NoticeId, string? NoticeNumber, string Code, string Message);

public sealed record NoticeServiceResultDto(int Recorded, IReadOnlyList<NoticeServiceFailureDto> Failures);

/// <summary>
/// Whether a city/municipality's general revision assessment roll may be run (GRI 17; Q15): every unit posted and declared by
/// an approved Tax Declaration (the roll lists TDs in force), every notice the units need served, and <see cref="WaitDays"/>
/// elapsed since the latest receipt.
/// </summary>
/// <param name="NoticesRequired">Posted units whose values changed (or that had no previous assessment), so LGC §223 requires notice.</param>
/// <param name="OpensOn">The latest receipt plus the waiting period; null while notices are outstanding.</param>
public sealed record RollGateDto(
    Guid MunicipalityId, string MunicipalityName, int Units, int UnitsNotPosted, int TaxDeclarationsNotApproved, int NoticesRequired, int NoticesServed, int NoticesOutstanding,
    DateOnly? LatestReceipt, int WaitDays, DateOnly? OpensOn, bool Open, IReadOnlyList<string> Blockers);

/// <param name="BarangayId">One barangay; omitted, every barangay with units in the revision (an Ownership Record Form run: every current owner).</param>
/// <param name="AsOf">Omitted: the revision's effective date.</param>
/// <param name="OverrideReason">An assessment roll run while its gate is closed (Q15): why; recorded on the run and printed on the roll.</param>
public sealed record CreateGeneralRevisionRegisterRunsRequest(RegisterKind Kind, Guid? BarangayId = null, DateOnly? AsOf = null, string? OverrideReason = null);

public sealed record GeneralRevisionRegisterRunDto(
    Guid Id, RegisterKind Kind, string FormCode, DateOnly AsOf, string? BarangayName, string? TaxpayerName, string? RollGateOverrideReason, DateTimeOffset CreatedAt);

public interface IGeneralRevisionRecordsService
{
    /// <summary>The Notices of Assessment that list the revision's assessments.</summary>
    Task<Result<PagedResult<GeneralRevisionNoticeDto>>> SearchNoticesAsync(Guid id, GeneralRevisionNoticeSearchRequest request, CancellationToken cancellationToken = default);
    /// <summary>Records service of several issued notices at once, notice by notice through the ordinary service call.</summary>
    Task<Result<NoticeServiceResultDto>> RecordNoticeServiceAsync(Guid id, RecordGeneralRevisionNoticeServiceRequest request, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<RollGateDto>>> RollGatesAsync(Guid id, CancellationToken cancellationToken = default);
    /// <summary>Register runs for the revision: assessment rolls (behind the gate), pre- and post-TMCR, Ownership Record Forms.</summary>
    Task<Result<IReadOnlyList<GeneralRevisionRegisterRunDto>>> CreateRegisterRunsAsync(Guid id, CreateGeneralRevisionRegisterRunsRequest request,
        CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<GeneralRevisionRegisterRunDto>>> ListRegisterRunsAsync(Guid id, CancellationToken cancellationToken = default);
}

/// <summary>
/// The general revision's notices and records (docs/analysis/smv-preparation-general-revision.md §4.6, L6-6c): the notices of
/// its posted units, their service in bulk, the 60-day gate on its assessment roll (Q15), and its register runs.
/// </summary>
public sealed class GeneralRevisionRecordsService(
    IApplicationDbContext db, IClock clock, IJurisdiction jurisdiction, INoticeService notices, IRegisterService registers,
    IOptions<GeneralRevisionOptions> options) : IGeneralRevisionRecordsService
{
    public const int MaxServiceBatch = 500;

    public async Task<Result<PagedResult<GeneralRevisionNoticeDto>>> SearchNoticesAsync(Guid id, GeneralRevisionNoticeSearchRequest r,
        CancellationToken cancellationToken = default)
    {
        if (await ProgrammeAsync(id, cancellationToken) is { IsFailure: true } missing)
        {
            return Result.Failure<PagedResult<GeneralRevisionNoticeDto>>(missing.Code!, missing.Message!);
        }
        var query = Notices(id);
        if (r.Status is { } s) query = query.Where(n => n.Status == s);
        if (!string.IsNullOrWhiteSpace(r.Search))
        {
            var term = r.Search.Trim().ToLower();
            query = query.Where(n => n.AddresseeNames.ToLower().Contains(term) || (n.NoticeNumber != null && n.NoticeNumber.ToLower().Contains(term)));
        }
        var total = await query.CountAsync(cancellationToken);
        var today = clock.Today;
        var rows = await query.OrderBy(n => n.Property!.PropertyIdentificationNumber).ThenBy(n => n.CreatedAt)
            .Skip((r.Page - 1) * r.PageSize).Take(r.PageSize)
            .Select(n => new GeneralRevisionNoticeDto(n.Id, n.NoticeNumber, n.AddresseeNames, n.Items.Count, n.Property!.PropertyIdentificationNumber,
                n.AssessedValue, n.Status, n.IssueDueDate, n.Status == NoticeStatus.Draft && today > n.IssueDueDate, n.IssuedAt, n.ServiceMode,
                n.ReceivedDate, n.AppealDeadline))
            .ToListAsync(cancellationToken);
        return Result.Success(new PagedResult<GeneralRevisionNoticeDto> { Items = rows, TotalCount = total, Page = r.Page, PageSize = r.PageSize });
    }

    public async Task<Result<NoticeServiceResultDto>> RecordNoticeServiceAsync(Guid id, RecordGeneralRevisionNoticeServiceRequest r,
        CancellationToken cancellationToken = default)
    {
        var ids = (r.NoticeIds ?? []).Distinct().ToList();
        if (ids.Count == 0 || ids.Count > MaxServiceBatch || r.ServedTo?.Length > 300)
        {
            return Result.Failure<NoticeServiceResultDto>("VALIDATION_FAILED", $"Choose 1 to {MaxServiceBatch} notices; served to max 300.");
        }
        if (await ProgrammeAsync(id, cancellationToken) is { IsFailure: true } missing)
        {
            return Result.Failure<NoticeServiceResultDto>(missing.Code!, missing.Message!);
        }
        var found = await Notices(id).Where(n => ids.Contains(n.Id)).Select(n => new { n.Id, n.NoticeNumber, n.AddresseeNames }).ToListAsync(cancellationToken);
        if (found.Count != ids.Count)
        {
            return Result.Failure<NoticeServiceResultDto>("NOTICE_NOT_FOUND", "A notice is not one of this revision's.");
        }
        var failures = new List<NoticeServiceFailureDto>();
        var recorded = 0;
        foreach (var n in found.OrderBy(n => n.NoticeNumber))
        {
            var servedTo = string.IsNullOrWhiteSpace(r.ServedTo) ? n.AddresseeNames : r.ServedTo.Trim();
            var result = await notices.RecordServiceAsync(n.Id, new RecordNoticeServiceRequest(r.ServiceMode, r.ReceivedDate,
                servedTo.Length <= 300 ? servedTo : servedTo[..300], r.ProofReference, r.Notes, null, r.SentDate), cancellationToken);
            if (result.IsSuccess)
            {
                recorded++;
            }
            else
            {
                db.ClearChangeTracker();
                failures.Add(new NoticeServiceFailureDto(n.Id, n.NoticeNumber, result.Code ?? "FAILED", result.Message ?? "Refused."));
            }
        }
        return Result.Success(new NoticeServiceResultDto(recorded, failures));
    }

    public async Task<Result<IReadOnlyList<RollGateDto>>> RollGatesAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var programme = await ProgrammeAsync(id, cancellationToken);
        if (programme.IsFailure)
        {
            return Result.Failure<IReadOnlyList<RollGateDto>>(programme.Code!, programme.Message!);
        }
        var gates = new List<RollGateDto>();
        foreach (var scope in programme.Value.Scope.OrderBy(s => s.Municipality!.Name))
        {
            gates.Add(await GateAsync(id, scope.MunicipalityId, scope.Municipality!.Name, cancellationToken));
        }
        return Result.Success<IReadOnlyList<RollGateDto>>(gates);
    }

    internal async Task<RollGateDto> GateAsync(Guid id, Guid municipalityId, string name, CancellationToken ct)
    {
        var items = db.GeneralRevisionItems.AsNoTracking()
            .Where(x => x.GeneralRevisionProgrammeId == id && x.MunicipalityId == municipalityId && x.Status != GeneralRevisionItemStatus.Excluded);
        var units = await items.CountAsync(ct);
        var notPosted = await items.CountAsync(x => x.Assessment == null || x.Assessment.Status != WorkflowStatus.Posted, ct);
        var undeclared = await items.CountAsync(x => x.Assessment != null && x.Assessment.Status == WorkflowStatus.Posted
            && !db.TaxDeclarations.Any(t => t.AssessmentId == x.AssessmentId && t.Status == WorkflowStatus.Approved), ct);
        var required = items.Where(x => x.Assessment != null && x.Assessment.Status == WorkflowStatus.Posted
            && (x.PreviousAssessmentId == null || x.PreviousAssessedValue != x.NewAssessedValue));
        var requiredCount = await required.CountAsync(ct);
        // A unit's notice is served when a served notice (not cancelled) lists its assessment.
        var served = required.Select(x => db.NoticeOfAssessmentItems
            .Where(i => i.AssessmentId == x.AssessmentId)
            .Join(db.NoticesOfAssessment.Where(n => n.Status == NoticeStatus.Served), i => i.NoticeOfAssessmentId, n => n.Id, (i, n) => n.ReceivedDate)
            .Max());
        var receipts = await served.ToListAsync(ct);
        var servedCount = receipts.Count(d => d is not null);
        var latest = receipts.Max();
        var wait = options.Value.RollWaitDays;
        var outstanding = requiredCount - servedCount;
        DateOnly? opensOn = outstanding == 0 && latest is { } l ? l.AddDays(wait) : null;
        var blockers = new List<string>();
        if (units == 0) blockers.Add("No unit of this city/municipality is in the revision.");
        if (notPosted > 0) blockers.Add($"{notPosted} unit(s) not yet posted.");
        if (undeclared > 0) blockers.Add($"{undeclared} posted unit(s) whose new Tax Declaration is not approved.");
        if (outstanding > 0) blockers.Add($"{outstanding} unit(s) whose notice is not yet served.");
        if (opensOn is { } on && clock.Today < on) blockers.Add($"The {wait}-day period after the latest receipt ({latest:yyyy-MM-dd}) ends on {on.AddDays(-1):yyyy-MM-dd}.");
        return new RollGateDto(municipalityId, name, units, notPosted, undeclared, requiredCount, servedCount, outstanding, latest, wait, opensOn, blockers.Count == 0, blockers);
    }

    public async Task<Result<IReadOnlyList<GeneralRevisionRegisterRunDto>>> CreateRegisterRunsAsync(Guid id, CreateGeneralRevisionRegisterRunsRequest r,
        CancellationToken cancellationToken = default)
    {
        var ct = cancellationToken;
        var reason = string.IsNullOrWhiteSpace(r.OverrideReason) ? null : r.OverrideReason.Trim();
        var roll = r.Kind is RegisterKind.AssessmentRollTaxable or RegisterKind.AssessmentRollExempt;
        if (!Enum.IsDefined(r.Kind) || r.Kind == RegisterKind.RecordOfAssessment || reason?.Length > 500 || (reason is not null && !roll)
            || (r.Kind == RegisterKind.OwnershipRecordCard && r.BarangayId is not null))
        {
            return Fail("VALIDATION_FAILED",
                "Assessment rolls, pre- and post-TMCR or Ownership Record Forms (for every owner); an override reason (max 500) goes only with an assessment roll.");
        }
        var programme = await ProgrammeAsync(id, ct);
        if (programme.IsFailure)
        {
            return Fail(programme.Code!, programme.Message!);
        }
        var p = programme.Value;
        if (p.Status is GeneralRevisionStatus.Completed or GeneralRevisionStatus.Cancelled)
        {
            return Fail("GENERAL_REVISION_CLOSED", $"The revision is {p.Status}.");
        }
        var asOf = r.AsOf ?? p.EffectiveDate;
        var items = db.GeneralRevisionItems.AsNoTracking().Where(x => x.GeneralRevisionProgrammeId == id);
        var barangays = await items.Where(x => x.Status != GeneralRevisionItemStatus.Excluded && (r.BarangayId == null || x.BarangayId == r.BarangayId))
            .Select(x => new { x.BarangayId, x.MunicipalityId }).Distinct().ToListAsync(ct);
        if (barangays.Count == 0)
        {
            return Fail("GENERAL_REVISION_ITEM_NOT_FOUND", r.BarangayId is null ? "The revision has no units yet." : "No unit of the revision is in that barangay.");
        }
        // GRI 17: the roll waits for the notices (Q15), per city/municipality; an override is recorded with its reason.
        if (roll)
        {
            foreach (var m in barangays.Select(b => b.MunicipalityId).Distinct())
            {
                var name = p.Scope.First(s => s.MunicipalityId == m).Municipality!.Name;
                var gate = await GateAsync(id, m, name, ct);
                if (!gate.Open && reason is null)
                {
                    return Fail("ROLL_GATE_CLOSED", $"{name}: {string.Join(" ", gate.Blockers)} Give a reason to run the roll anyway.");
                }
                if (gate.Open)
                {
                    reason = null; // nothing to override
                }
            }
        }
        var remarks = $"General revision {p.RevisionYear}" + (reason is null ? "" : $". Run before every notice was served and the waiting period had passed: {reason}");
        var created = new List<Guid>();
        var ownsTransaction = db.Database.CurrentTransaction is null;
        await using var transaction = ownsTransaction ? await db.Database.BeginTransactionAsync(ct) : null;
        if (r.Kind == RegisterKind.OwnershipRecordCard)
        {
            // GRI 18: the forms of the units' current owners.
            var propertyIds = await items.Where(x => x.Assessment != null && x.Assessment.Status == WorkflowStatus.Posted).Select(x => x.PropertyId).Distinct().ToListAsync(ct);
            var owners = await db.PropertyTaxpayers.AsNoTracking()
                .Where(x => propertyIds.Contains(x.PropertyId) && x.IsCurrent && x.Role == PropertyPartyRole.Owner).Select(x => x.TaxpayerId).Distinct().ToListAsync(ct);
            if (owners.Count == 0)
            {
                return Fail("OWNER_HOLDS_NO_PROPERTY", "No posted unit of the revision has a current declared owner.");
            }
            foreach (var owner in owners)
            {
                var run = await registers.CreateRunAsync(new CreateRegisterRunRequest(r.Kind, asOf, null, null, owner, null, remarks), ct);
                if (run.IsFailure)
                {
                    return Fail(run.Code!, run.Message!);
                }
                created.Add(run.Value.Id);
            }
        }
        else
        {
            foreach (var b in barangays)
            {
                var run = await registers.CreateRunAsync(new CreateRegisterRunRequest(r.Kind, asOf, b.BarangayId, null, null, null, remarks), ct);
                if (run.IsFailure)
                {
                    return Fail(run.Code!, run.Message!);
                }
                created.Add(run.Value.Id);
            }
        }
        await db.RegisterRuns.Where(x => created.Contains(x.Id)).ExecuteUpdateAsync(s => s
            .SetProperty(x => x.GeneralRevisionProgrammeId, id).SetProperty(x => x.RollGateOverrideReason, reason), ct);
        if (transaction is not null)
        {
            await transaction.CommitAsync(ct);
        }
        return Result.Success(await Runs(db.RegisterRuns.Where(x => created.Contains(x.Id)), ct));
    }

    public async Task<Result<IReadOnlyList<GeneralRevisionRegisterRunDto>>> ListRegisterRunsAsync(Guid id, CancellationToken cancellationToken = default) =>
        await ProgrammeAsync(id, cancellationToken) is { IsFailure: true } missing
            ? Fail(missing.Code!, missing.Message!)
            : Result.Success(await Runs(db.RegisterRuns.Where(x => x.GeneralRevisionProgrammeId == id), cancellationToken));

    private static async Task<IReadOnlyList<GeneralRevisionRegisterRunDto>> Runs(IQueryable<Domain.Entities.Registers.RegisterRun> query, CancellationToken ct)
    {
        var rows = await query.AsNoTracking().OrderByDescending(x => x.CreatedAt).Take(500)
            .Select(x => new
            {
                x.Id, x.Kind, x.AsOf, Barangay = x.Barangay != null ? x.Barangay.Name : null, x.Taxpayer, x.RollGateOverrideReason, x.CreatedAt,
            }).ToListAsync(ct);
        return rows.Select(x => new GeneralRevisionRegisterRunDto(x.Id, x.Kind, RegisterService.FormCode(x.Kind), x.AsOf, x.Barangay,
            x.Taxpayer is { } t ? TaxpayerNameFormatter.Format(t.TaxpayerType, t.LastName, t.FirstName, t.MiddleName, t.Suffix, t.CorporateName) : null,
            x.RollGateOverrideReason, x.CreatedAt)).ToList();
    }

    /// <summary>The notices (not cancelled) that list an assessment of the revision's items.</summary>
    private IQueryable<NoticeOfAssessment> Notices(Guid id) =>
        db.NoticesOfAssessment.AsNoTracking().Where(n => n.Status != NoticeStatus.Cancelled
            && n.Items.Any(i => db.GeneralRevisionItems.Any(x => x.GeneralRevisionProgrammeId == id && x.AssessmentId == i.AssessmentId)));

    private async Task<Result<GeneralRevisionProgramme>> ProgrammeAsync(Guid id, CancellationToken ct)
    {
        var p = await db.GeneralRevisionProgrammes.AsNoTracking().Include(x => x.Scope).ThenInclude(s => s.Municipality).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (p is null)
        {
            return Result.Failure<GeneralRevisionProgramme>("GENERAL_REVISION_NOT_FOUND", "No general revision was found with the given id.");
        }
        return p.Scope.Any(s => !jurisdiction.Allows(s.MunicipalityId))
            ? Result.Failure<GeneralRevisionProgramme>(JurisdictionErrors.Code, "This revision covers a city/municipality outside your office's jurisdiction.")
            : Result.Success(p);
    }

    private static Result<IReadOnlyList<GeneralRevisionRegisterRunDto>> Fail(string code, string message) =>
        Result.Failure<IReadOnlyList<GeneralRevisionRegisterRunDto>>(code, message);
}
