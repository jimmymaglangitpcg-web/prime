using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Prime.Application.Common;
using Prime.Application.Common.Interfaces;
using Prime.Domain.DomainServices;
using Prime.Domain.Entities;
using Prime.Domain.Enums;

namespace Prime.Application.Features.Smv;

/// <summary>
/// The statutory periods of an SMV's preparation and review, in days (RA 12001 §§15–16 and its IRR; LAM 2025 Book IV
/// pp.104–107, 118–119; docs/analysis/smv-preparation-general-revision.md §4.2). Used for due dates and reminders only.
/// DOMAIN VERIFICATION REQUIRED against the IRR.
/// </summary>
public sealed class SmvPreparationOptions
{
    public const string SectionName = "Smv";

    /// <summary>Public consultations before submission; fewer gives a warning, not a refusal (RA 12001 §15).</summary>
    public int MinimumConsultations { get; set; } = 2;
    public int RegionalOfficeReviewDays { get; set; } = 45;
    public int BlgfReviewDays { get; set; } = 30;
    public int CertificationDays { get; set; } = 30;
    public int ResubmissionDays { get; set; } = 30;
    public int ResubmittedDecisionDays { get; set; } = 10;
    public int EffectivityDaysAfterPublication { get; set; } = 15;

    public SmvReviewPeriods Periods => new(RegionalOfficeReviewDays, BlgfReviewDays, CertificationDays, ResubmissionDays, ResubmittedDecisionDays,
        EffectivityDaysAfterPublication);
}

public sealed record CreateSmvPreparationRequest(int RevisionYear, string Title, DateOnly? DateOfValuation, DateOnly? BaseValuationDate,
    DateOnly PlannedEffectivityDate, IReadOnlyList<Guid>? MunicipalityIds, string? Notes);

public sealed record UpdateSmvPreparationRequest(string Title, DateOnly? DateOfValuation, DateOnly? BaseValuationDate, DateOnly PlannedEffectivityDate,
    string? Notes);

public sealed record AddSmvConsultationRequest(DateOnly HeldOn, SmvConsultationMode Mode, string? Venue, int? Attendance, string? MinutesReference, string? Notes);

public sealed record RecordSmvPreparationEventRequest(SmvPreparationEventKind Kind, DateOnly OccurredOn, string? Reference, string? Note);

public sealed record CancelSmvPreparationRequest(string Reason);

public sealed record SmvConsultationDto(Guid Id, DateOnly HeldOn, SmvConsultationMode Mode, string? Venue, int? Attendance, string? MinutesReference, string? Notes);

public sealed record SmvPreparationEventDto(Guid Id, SmvPreparationEventKind Kind, string Label, DateOnly OccurredOn, string? Reference, string? Note,
    DateTimeOffset RecordedAt);

public sealed record SmvPreparationSmvDto(Guid Id, string Reference, WorkflowStatus Status, DateOnly EffectivityDate, IReadOnlyList<string> Coverage,
    int ScheduleCount, DateOnly? ProposedOn, DateOnly? PublishedForCommentOn, DateOnly? ConsultationsHeldOn, DateOnly? SubmittedToBlgfOn,
    DateOnly? CertifiedOn, string? CertificationReference, DateOnly? PublishedOn, string? PublicationReference);

public sealed record SmvPreparationDto(
    Guid Id, int RevisionYear, string Title, DateOnly? DateOfValuation, DateOnly? BaseValuationDate, SmvPreparationStatus Status, string? Notes,
    string? CancellationReason, SmvPreparationSmvDto ProposedSmv, IReadOnlyList<SmvConsultationDto> Consultations,
    IReadOnlyList<SmvPreparationEventDto> Events, int MinimumConsultations, SmvPreparationDue? NextDue, bool Editable,
    IReadOnlyList<string> Warnings, uint RowVersion = 0);

public sealed record SmvPreparationSummaryDto(Guid Id, int RevisionYear, string Title, SmvPreparationStatus Status, Guid ProposedSmvId,
    DateOnly EffectivityDate, int ConsultationCount, DateTimeOffset CreatedAt);

public interface ISmvPreparationService
{
    /// <summary>Opens the work file and its proposed SMV (a Draft, certified basis, no reference yet).</summary>
    Task<Result<SmvPreparationDto>> CreateAsync(CreateSmvPreparationRequest request, CancellationToken cancellationToken = default);
    Task<Result<SmvPreparationDto>> UpdateAsync(Guid id, UpdateSmvPreparationRequest request, CancellationToken cancellationToken = default);
    Task<Result<SmvPreparationDto>> AddConsultationAsync(Guid id, AddSmvConsultationRequest request, CancellationToken cancellationToken = default);
    /// <summary>Records a step of the review; the result lists warnings (too few consultations, rows after the effectivity …).</summary>
    Task<Result<SmvPreparationDto>> RecordEventAsync(Guid id, RecordSmvPreparationEventRequest request, CancellationToken cancellationToken = default);
    Task<Result<SmvPreparationDto>> CancelAsync(Guid id, CancelSmvPreparationRequest request, CancellationToken cancellationToken = default);
    Task<Result<SmvPreparationDto>> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<SmvPreparationSummaryDto>>> ListAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// SMV preparation work files (docs/analysis/smv-preparation-general-revision.md §4.2). The Provincial Assessor's Office
/// prepares; municipal offices read (Q2). The work file is the only writer of its proposed SMV's stage dates, which it
/// writes while the SMV is not yet approved in PRIME; the SMV is approved (entered) only once the certified SMV is
/// published (<see cref="SmvService"/>).
/// </summary>
public sealed class SmvPreparationService(IApplicationDbContext db, IClock clock, IJurisdiction jurisdiction, IOptions<SmvPreparationOptions> options,
    ICurrentUserService currentUser)
    : ISmvPreparationService
{
    public const string ForbiddenCode = "SMV_PREPARATION_FORBIDDEN";
    private const string ForbiddenMessage = "The SMV is prepared by the Provincial Assessor's Office; municipal offices see the preparation only.";

    private static readonly SmvPreparationStatus[] Closed =
        [SmvPreparationStatus.Certified, SmvPreparationStatus.NotCertified, SmvPreparationStatus.Published, SmvPreparationStatus.Cancelled];

    public async Task<Result<SmvPreparationDto>> CreateAsync(CreateSmvPreparationRequest r, CancellationToken cancellationToken = default)
    {
        var ct = cancellationToken;
        if (jurisdiction.Restricted)
        {
            return Fail(ForbiddenCode, ForbiddenMessage);
        }
        var title = r.Title?.Trim();
        if (r.RevisionYear is < 1990 or > 2200 || string.IsNullOrEmpty(title) || title.Length > 300 || r.PlannedEffectivityDate == default
            || r.Notes?.Length > 4000)
        {
            return Fail("VALIDATION_FAILED", "A revision year, a title (max 300) and the planned effectivity are required; notes max 4000.");
        }
        var municipalities = (r.MunicipalityIds ?? []).Distinct().ToList();
        if (municipalities.Count > 0 && await db.Municipalities.CountAsync(x => municipalities.Contains(x.Id), ct) != municipalities.Count)
        {
            return Fail("MUNICIPALITY_NOT_FOUND", "A city/municipality in the coverage does not exist.");
        }
        if (await db.SmvPreparations.AnyAsync(x => x.RevisionYear == r.RevisionYear && x.Status != SmvPreparationStatus.Cancelled, ct))
        {
            return Fail("SMV_PREPARATION_DUPLICATE", $"An SMV for {r.RevisionYear} is already being prepared.");
        }
        var smv = new Domain.Entities.Smv
        {
            Basis = SmvBasis.Certified, RevisionYear = r.RevisionYear, EffectivityDate = r.PlannedEffectivityDate, ProposedOn = clock.Today,
            Description = $"Proposed SMV — {title}", Status = WorkflowStatus.Draft,
            Coverage = municipalities.Select(m => new SmvCoverage { MunicipalityId = m }).ToList(),
        };
        var preparation = new SmvPreparation
        {
            RevisionYear = r.RevisionYear, Title = title, DateOfValuation = r.DateOfValuation, BaseValuationDate = r.BaseValuationDate,
            ProposedSmv = smv, Notes = Clean(r.Notes),
        };
        db.SmvPreparations.Add(preparation);
        await db.SaveChangesAsync(ct);
        return await GetAsync(preparation.Id, ct);
    }

    public async Task<Result<SmvPreparationDto>> UpdateAsync(Guid id, UpdateSmvPreparationRequest r, CancellationToken cancellationToken = default)
    {
        var title = r.Title?.Trim();
        if (string.IsNullOrEmpty(title) || title.Length > 300 || r.PlannedEffectivityDate == default || r.Notes?.Length > 4000)
        {
            return Fail("VALIDATION_FAILED", "A title (max 300) and the planned effectivity are required; notes max 4000.");
        }
        var opened = await OpenAsync(id, cancellationToken);
        if (opened.IsFailure)
        {
            return Fail(opened.Code!, opened.Message!);
        }
        var p = opened.Value;
        (p.Title, p.DateOfValuation, p.BaseValuationDate, p.Notes) = (title, r.DateOfValuation, r.BaseValuationDate, Clean(r.Notes));
        p.ProposedSmv!.EffectivityDate = r.PlannedEffectivityDate;
        await db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    public async Task<Result<SmvPreparationDto>> AddConsultationAsync(Guid id, AddSmvConsultationRequest r, CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(r.Mode) || r.HeldOn == default || r.Attendance < 0 || r.Venue?.Length > 300 || r.MinutesReference?.Length > 200
            || r.Notes?.Length > 2000)
        {
            return Fail("VALIDATION_FAILED", "A date and a mode are required; attendance not negative; venue max 300, minutes reference max 200, notes max 2000.");
        }
        if (r.HeldOn > clock.Today)
        {
            return Fail("DATE_IN_FUTURE", "A consultation is recorded once it has been held.");
        }
        var opened = await OpenAsync(id, cancellationToken);
        if (opened.IsFailure)
        {
            return Fail(opened.Code!, opened.Message!);
        }
        var p = opened.Value;
        // Consultations come before submission; after a remand the office may hold one more (Book IV p.119).
        if (p.Status is not (SmvPreparationStatus.Preparing or SmvPreparationStatus.PublishedForComment or SmvPreparationStatus.Remanded))
        {
            return Fail("SMV_PREPARATION_STAGE", $"Consultations are recorded before submission, or after a remand; the preparation is {p.Status}.");
        }
        db.SmvConsultations.Add(new SmvConsultation
        {
            SmvPreparationId = id, HeldOn = r.HeldOn, Mode = r.Mode, Venue = Clean(r.Venue), Attendance = r.Attendance,
            MinutesReference = Clean(r.MinutesReference), Notes = Clean(r.Notes),
        });
        var latest = p.Consultations.Select(c => c.HeldOn).Append(r.HeldOn).Max();
        p.ProposedSmv!.ConsultationsHeldOn = latest;
        await db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    public async Task<Result<SmvPreparationDto>> RecordEventAsync(Guid id, RecordSmvPreparationEventRequest r, CancellationToken cancellationToken = default)
    {
        var ct = cancellationToken;
        var reference = Clean(r.Reference);
        var note = Clean(r.Note);
        if (!Enum.IsDefined(r.Kind) || r.OccurredOn == default || reference?.Length > 200 || note?.Length > 4000)
        {
            return Fail("VALIDATION_FAILED", "A kind and a date are required; reference max 200, note max 4000.");
        }
        if (r.OccurredOn > clock.Today)
        {
            return Fail("DATE_IN_FUTURE", "A step is recorded once it has happened.");
        }
        if (r.Kind == SmvPreparationEventKind.Remanded && note is null)
        {
            return Fail("REMAND_REASONS_REQUIRED", "Record the reasons for the remand.");
        }
        if (r.Kind is SmvPreparationEventKind.Certified or SmvPreparationEventKind.Published && reference is null)
        {
            return Fail("REFERENCE_REQUIRED", r.Kind == SmvPreparationEventKind.Certified
                ? "Record the certification's reference." : "Record where the certified SMV was published.");
        }
        if (jurisdiction.Restricted)
        {
            return Fail(ForbiddenCode, ForbiddenMessage);
        }
        var p = await Load(db.SmvPreparations).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (p is null)
        {
            return Fail("SMV_PREPARATION_NOT_FOUND", "No SMV preparation was found with the given id.");
        }
        var (next, problem) = p.Status == SmvPreparationStatus.Cancelled
            ? (null, "The preparation was cancelled.")
            : SmvPreparationFlow.Apply(p.Status, r.Kind);
        if (next is null)
        {
            return Fail("SMV_PREPARATION_STAGE", problem!);
        }
        if (p.Events.Count > 0 && r.OccurredOn < p.Events.Max(e => e.OccurredOn))
        {
            return Fail("SMV_PREPARATION_DATE_ORDER", $"The step cannot be dated before the latest one recorded ({p.Events.Max(e => e.OccurredOn):yyyy-MM-dd}).");
        }
        var smv = p.ProposedSmv!;
        if (smv.Status != WorkflowStatus.Draft && r.Kind != SmvPreparationEventKind.TransmittedToSanggunian)
        {
            return Fail("SMV_NOT_DRAFT", "The proposed SMV is no longer a draft in PRIME; its stage dates can no longer change.");
        }
        var warnings = new List<string>();
        switch (r.Kind)
        {
            case SmvPreparationEventKind.PublishedForComment:
                smv.PublishedForCommentOn = r.OccurredOn;
                break;
            case SmvPreparationEventKind.SubmittedToRegionalOffice:
                if (p.Consultations.Count < options.Value.MinimumConsultations)
                {
                    warnings.Add($"Only {p.Consultations.Count} public consultation(s) recorded; at least {options.Value.MinimumConsultations} are required before submission.");
                }
                if (smv.PublishedForCommentOn is null)
                {
                    warnings.Add("The proposed SMV was not recorded as published for comment.");
                }
                // The base valuation date is the date of submission (Book IV p.112).
                if (p.BaseValuationDate is { } planned && planned != r.OccurredOn)
                {
                    warnings.Add($"The base valuation date was {planned:yyyy-MM-dd}; it is now the date of submission, {r.OccurredOn:yyyy-MM-dd}. Sales adjusted to the earlier date need adjusting again.");
                }
                p.BaseValuationDate = r.OccurredOn;
                smv.SubmittedToBlgfOn = r.OccurredOn;
                break;
            case SmvPreparationEventKind.Resubmitted:
                smv.SubmittedToBlgfOn = r.OccurredOn;
                break;
            case SmvPreparationEventKind.Certified:
                if (await db.Smvs.AnyAsync(x => x.CertificationReference == reference && x.Id != smv.Id, ct))
                {
                    return Fail("SMV_CERTIFICATION_DUPLICATE", $"An SMV with certification '{reference}' already exists.");
                }
                (smv.CertifiedOn, smv.CertificationReference) = (r.OccurredOn, reference);
                break;
            case SmvPreparationEventKind.Published:
                (smv.PublishedOn, smv.PublicationReference) = (r.OccurredOn, reference);
                smv.EffectivityDate = SmvPreparationFlow.Effectivity(r.OccurredOn, options.Value.Periods);
                var later = await db.SmvSchedules.CountAsync(x => x.SmvId == smv.Id && x.EndDate == null && x.EffectiveDate > smv.EffectivityDate, ct);
                if (later > 0)
                {
                    warnings.Add($"The SMV takes effect on {smv.EffectivityDate:yyyy-MM-dd}, but {later} of its rows take effect later; units are not valued under them before then.");
                }
                break;
        }
        p.Status = next.Value;
        db.SmvPreparationEvents.Add(new SmvPreparationEvent { SmvPreparationId = id, Kind = r.Kind, OccurredOn = r.OccurredOn, Reference = reference, Note = note });
        await db.SaveChangesAsync(ct);
        var dto = await GetAsync(id, ct);
        return dto.IsFailure ? dto : Result.Success(dto.Value with { Warnings = warnings });
    }

    public async Task<Result<SmvPreparationDto>> CancelAsync(Guid id, CancelSmvPreparationRequest r, CancellationToken cancellationToken = default)
    {
        var reason = Clean(r.Reason);
        if (reason is null || reason.Length > 1000)
        {
            return Fail("VALIDATION_FAILED", "A reason (max 1000) is required.");
        }
        var opened = await OpenAsync(id, cancellationToken);
        if (opened.IsFailure)
        {
            return Fail(opened.Code!, opened.Message!);
        }
        var p = opened.Value;
        (p.Status, p.CancelledAt, p.CancellationReason) = (SmvPreparationStatus.Cancelled, clock.UtcNow, reason);
        p.ProposedSmv!.Status = WorkflowStatus.Cancelled;
        currentUser.Reason = reason;
        await db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    public async Task<Result<SmvPreparationDto>> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var p = await Load(db.SmvPreparations.AsNoTracking()).Include(x => x.ProposedSmv!.Coverage).ThenInclude(c => c.Municipality)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (p is null)
        {
            return Fail("SMV_PREPARATION_NOT_FOUND", "No SMV preparation was found with the given id.");
        }
        var smv = p.ProposedSmv!;
        var rows = await db.SmvSchedules.CountAsync(x => x.SmvId == smv.Id && x.EndDate == null, cancellationToken);
        var open = !Closed.Contains(p.Status);
        return Result.Success(new SmvPreparationDto(
            p.Id, p.RevisionYear, p.Title, p.DateOfValuation, p.BaseValuationDate, p.Status, p.Notes, p.CancellationReason,
            new SmvPreparationSmvDto(smv.Id, smv.Reference, smv.Status, smv.EffectivityDate,
                smv.Coverage.Select(c => c.Municipality?.Name ?? "").Order().ToList(), rows, smv.ProposedOn, smv.PublishedForCommentOn,
                smv.ConsultationsHeldOn, smv.SubmittedToBlgfOn, smv.CertifiedOn, smv.CertificationReference, smv.PublishedOn, smv.PublicationReference),
            p.Consultations.OrderBy(c => c.HeldOn).Select(c => new SmvConsultationDto(c.Id, c.HeldOn, c.Mode, c.Venue, c.Attendance, c.MinutesReference, c.Notes)).ToList(),
            p.Events.OrderBy(e => e.OccurredOn).ThenBy(e => e.CreatedAt)
                .Select(e => new SmvPreparationEventDto(e.Id, e.Kind, SmvPreparationFlow.Label(e.Kind), e.OccurredOn, e.Reference, e.Note, e.CreatedAt)).ToList(),
            options.Value.MinimumConsultations,
            p.Status == SmvPreparationStatus.Cancelled ? null : SmvPreparationFlow.NextDue(p.Events, options.Value.Periods),
            open && !jurisdiction.Restricted, [], p.RowVersion));
    }

    public async Task<Result<IReadOnlyList<SmvPreparationSummaryDto>>> ListAsync(CancellationToken cancellationToken = default) =>
        Result.Success<IReadOnlyList<SmvPreparationSummaryDto>>(await db.SmvPreparations.AsNoTracking()
            .OrderByDescending(x => x.RevisionYear).ThenByDescending(x => x.CreatedAt)
            .Select(x => new SmvPreparationSummaryDto(x.Id, x.RevisionYear, x.Title, x.Status, x.ProposedSmvId, x.ProposedSmv!.EffectivityDate,
                x.Consultations.Count, x.CreatedAt))
            .ToListAsync(cancellationToken));

    /// <summary>A preparation the user may change: provincial office, not closed, its SMV still a draft.</summary>
    private async Task<Result<SmvPreparation>> OpenAsync(Guid id, CancellationToken ct)
    {
        if (jurisdiction.Restricted)
        {
            return Result.Failure<SmvPreparation>(ForbiddenCode, ForbiddenMessage);
        }
        var p = await Load(db.SmvPreparations).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (p is null)
        {
            return Result.Failure<SmvPreparation>("SMV_PREPARATION_NOT_FOUND", "No SMV preparation was found with the given id.");
        }
        if (Closed.Contains(p.Status))
        {
            return Result.Failure<SmvPreparation>("SMV_PREPARATION_CLOSED", $"The preparation is {p.Status}; it can no longer be changed.");
        }
        return Result.Success(p);
    }

    private static IQueryable<SmvPreparation> Load(IQueryable<SmvPreparation> query) =>
        query.Include(x => x.ProposedSmv).Include(x => x.Consultations).Include(x => x.Events);

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static Result<SmvPreparationDto> Fail(string code, string message) => Result.Failure<SmvPreparationDto>(code, message);
}
