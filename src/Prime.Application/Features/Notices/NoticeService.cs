using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Prime.Application.Common;
using Prime.Application.Common.Interfaces;
using Prime.Application.Features.Numbering;
using Prime.Application.Features.Properties;
using Prime.Domain.Entities;
using Prime.Domain.Entities.Notices;
using Prime.Domain.Enums;

namespace Prime.Application.Features.Notices;

/// <summary>
/// "Notices" configuration section — statutory periods applied to Notices of
/// Assessment, each with its citation (CLAUDE.md §7). Every notice freezes
/// the values it used.
/// </summary>
public sealed class NoticeOptions
{
    public const string SectionName = "Notices";

    /// <summary>LGC §223: notice "within thirty (30) days" of the assessment.</summary>
    public int? IssuePeriodDays { get; set; }
    public string? IssuePeriodLegalBasis { get; set; }

    /// <summary>LGC §226: appeal "within sixty (60) days from the date of receipt of the written notice".</summary>
    public int? AppealPeriodDays { get; set; }
    public string? AppealPeriodLegalBasis { get; set; }
}

/// <summary>
/// <paramref name="Reason"/>: null derives it from the values (LGC §223: first
/// assessment, increase, decrease); one of the MRPAAO's descriptive reasons
/// (declared owner, owner's address, location changed) gives notice though the
/// value is unchanged (p.168).
/// </summary>
public sealed record GenerateNoticeRequest(Guid AssessmentId, NoticeReason? Reason = null);

/// <summary>One notice to one declared owner for several of the owner's posted assessments (MRPAAO Att. 10).</summary>
public sealed record GenerateCombinedNoticeRequest(Guid TaxpayerId, IReadOnlyList<Guid> AssessmentIds);

/// <summary>A posted assessment of a declared owner that has no notice yet and needs one.</summary>
public sealed record NoticeCandidateDto(
    Guid AssessmentId, Guid PropertyId, string Pin, Guid RpuId, string RpuNumber, int AssessmentYear, decimal AssessedValue, NoticeReason Reason);

public sealed record NoticeItemDto(
    int Sequence, Guid PropertyId, Guid RpuId, Guid AssessmentId, Guid? TaxDeclarationId, NoticeReason Reason,
    decimal? PreviousAssessedValue, decimal AssessedValue, decimal MarketValue, int AssessmentYear, DateOnly AssessmentEffectiveDate);

public sealed record RecordNoticeServiceRequest(NoticeServiceMode ServiceMode, DateOnly ReceivedDate, string ServedTo, string ProofReference, string? Notes);

public sealed record NoticeReasonRequest(string Reason);

public sealed record NoticeDto(
    Guid Id, string? NoticeNumber, Guid PropertyId, Guid RpuId, Guid AssessmentId, Guid? TaxDeclarationId,
    NoticeReason Reason, decimal? PreviousAssessedValue, decimal AssessedValue, decimal MarketValue,
    int AssessmentYear, DateOnly AssessmentEffectiveDate, string AddresseeNames, string? AddresseeAddress,
    int IssuePeriodDays, DateOnly IssueDueDate, bool IssueOverdue, int AppealPeriodDays,
    NoticeStatus Status, DateTimeOffset CreatedAt, DateTimeOffset? IssuedAt,
    NoticeServiceMode? ServiceMode, DateOnly? ReceivedDate, string? ServedTo, string? ProofReference, string? ServiceNotes,
    DateOnly? AppealDeadline, DateTimeOffset? CancelledAt, string? CancellationReason,
    Guid? AddresseeTaxpayerId = null, IReadOnlyList<NoticeItemDto>? Items = null);

public interface INoticeService
{
    /// <summary>
    /// A Draft notice for a posted assessment, when LGC §223 requires one: a
    /// first assessment, or an increase or decrease against the previous assessment.
    /// </summary>
    Task<Result<NoticeDto>> GenerateAsync(GenerateNoticeRequest request, CancellationToken cancellationToken = default);
    Task<Result<NoticeDto>> GenerateCombinedAsync(GenerateCombinedNoticeRequest request, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<NoticeCandidateDto>>> CandidatesAsync(Guid taxpayerId, CancellationToken cancellationToken = default);
    /// <summary>Marks the notice issued and numbers it (NoticeOfAssessment scheme, if any).</summary>
    Task<Result<NoticeDto>> IssueAsync(Guid id, CancellationToken cancellationToken = default);
    /// <summary>Records service (mode, receipt date, proof) and fixes the appeal deadline.</summary>
    Task<Result<NoticeDto>> RecordServiceAsync(Guid id, RecordNoticeServiceRequest request, CancellationToken cancellationToken = default);
    Task<Result<NoticeDto>> CancelAsync(Guid id, string reason, CancellationToken cancellationToken = default);
    Task<Result<NoticeDto>> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<NoticeDto>>> ListByPropertyAsync(Guid propertyId, CancellationToken cancellationToken = default);
}

/// <summary>docs/FORMS-REVISION-PLAN.md A6; LGC §§223, 226.</summary>
public sealed class NoticeService(
    IApplicationDbContext db,
    ICurrentUserService currentUser,
    INumberingService numbering,
    IClock clock,
    IOptions<NoticeOptions> options) : INoticeService
{
    public async Task<Result<NoticeDto>> GenerateAsync(GenerateNoticeRequest request, CancellationToken cancellationToken = default)
    {
        var item = await ItemAsync(request.AssessmentId, request.Reason, cancellationToken);
        if (item.IsFailure)
        {
            return Fail(item.Code!, item.Message!);
        }
        var (assessment, line) = item.Value;
        var parties = await PropertyParties.ProjectAsync(
            (await PropertyParties.ScopeAsync(db, assessment.PropertyId, assessment.RpuId, x => x.IsCurrent, cancellationToken))
                .Where(x => x.Role != PropertyPartyRole.LegalInterestHolder), cancellationToken);
        if (parties.Count == 0)
        {
            return Fail("NOTICE_NO_ADDRESSEE",
                "The property has no current declared party (owner, administrator, beneficial user, claimant or unknown owner) to address the notice to.");
        }
        var notice = Header([(assessment, line)],
            string.Join("; ", parties.Select(p => p.Role is PropertyPartyRole.Owner or PropertyPartyRole.UnknownOwner
                ? p.TaxpayerDisplayName
                : $"{p.TaxpayerDisplayName} ({PropertyParties.RoleLabel(p.Role)})")),
            parties.Select(p => p.Address).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)), null);
        db.NoticesOfAssessment.Add(notice);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success(ToDto(notice));
    }

    /// <summary>
    /// One notice to one declared owner listing several of the owner's posted
    /// assessments (MRPAAO Att. 10), each needing notice for its values. The
    /// owner must be a current owner of every unit; the appeal period then runs
    /// from the one receipt.
    /// </summary>
    public async Task<Result<NoticeDto>> GenerateCombinedAsync(GenerateCombinedNoticeRequest request, CancellationToken cancellationToken = default)
    {
        var ids = request.AssessmentIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return Fail("VALIDATION_FAILED", "Name at least one assessment.");
        }
        var taxpayer = await db.Taxpayers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == request.TaxpayerId, cancellationToken);
        if (taxpayer is null)
        {
            return Fail("TAXPAYER_NOT_FOUND", "No taxpayer was found with the given id.");
        }
        var items = new List<(Assessment, NoticeOfAssessmentItem)>();
        foreach (var id in ids)
        {
            var item = await ItemAsync(id, null, cancellationToken);
            if (item.IsFailure)
            {
                return Fail(item.Code!, $"Assessment {id}: {item.Message}");
            }
            var (assessment, _) = item.Value;
            var owns = await (await PropertyParties.ScopeAsync(db, assessment.PropertyId, assessment.RpuId, x => x.IsCurrent, cancellationToken))
                .AnyAsync(x => x.TaxpayerId == taxpayer.Id && x.Role == PropertyPartyRole.Owner, cancellationToken);
            if (!owns)
            {
                return Fail("NOTICE_ADDRESSEE_NOT_OWNER", $"The taxpayer is not a current declared owner of assessment {id}'s unit.");
            }
            items.Add(item.Value);
        }
        var notice = Header(items, TaxpayerNameFormatter.Format(taxpayer.TaxpayerType, taxpayer.LastName, taxpayer.FirstName, taxpayer.MiddleName,
            taxpayer.Suffix, taxpayer.CorporateName), taxpayer.Address, taxpayer.Id);
        db.NoticesOfAssessment.Add(notice);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success(ToDto(notice));
    }

    /// <summary>The declared owner's posted assessments that need a notice for their values and have none yet.</summary>
    public async Task<Result<IReadOnlyList<NoticeCandidateDto>>> CandidatesAsync(Guid taxpayerId, CancellationToken cancellationToken = default)
    {
        var owned = await db.PropertyTaxpayers.AsNoTracking()
            .Where(x => x.TaxpayerId == taxpayerId && x.IsCurrent && x.Role == PropertyPartyRole.Owner)
            .Select(x => new { x.PropertyId, x.RpuId }).ToListAsync(cancellationToken);
        var propertyIds = owned.Select(x => x.PropertyId).Distinct().ToList();
        var assessments = await db.Assessments.AsNoTracking().Include(x => x.Rpu).Include(x => x.Property)
            .Where(x => propertyIds.Contains(x.PropertyId) && x.Status == WorkflowStatus.Posted
                && !db.NoticeOfAssessmentItems.Any(i => i.AssessmentId == x.Id
                    && db.NoticesOfAssessment.Any(n => n.Id == i.NoticeOfAssessmentId && n.Status != NoticeStatus.Cancelled)))
            .OrderBy(x => x.Property!.PropertyIdentificationNumber).ThenBy(x => x.Rpu!.RpuNumber).ThenByDescending(x => x.EffectiveDate)
            .ToListAsync(cancellationToken);
        var result = new List<NoticeCandidateDto>();
        foreach (var a in assessments)
        {
            // The owner must hold the unit: its own owners, else the property's.
            var owns = await (await PropertyParties.ScopeAsync(db, a.PropertyId, a.RpuId, x => x.IsCurrent, cancellationToken))
                .AnyAsync(x => x.TaxpayerId == taxpayerId && x.Role == PropertyPartyRole.Owner, cancellationToken);
            var reason = owns ? await ValueReasonAsync(a, cancellationToken) : null;
            if (reason is { } r)
            {
                result.Add(new NoticeCandidateDto(a.Id, a.PropertyId, a.Property!.PropertyIdentificationNumber, a.RpuId, a.Rpu!.RpuNumber,
                    a.AssessmentYear, a.AssessedValue, r));
            }
        }
        return Result.Success<IReadOnlyList<NoticeCandidateDto>>(result);
    }

    private async Task<NoticeReason?> ValueReasonAsync(Assessment assessment, CancellationToken ct)
    {
        var previous = await AssessmentHistory.PreviousAsync(db, assessment, ct);
        if (previous is null)
        {
            return NoticeReason.FirstAssessment;
        }
        return assessment.AssessedValue > previous.AssessedValue ? NoticeReason.AssessmentIncreased
            : assessment.AssessedValue < previous.AssessedValue ? NoticeReason.AssessmentDecreased
            : null;
    }

    /// <summary>
    /// The notice row for one posted assessment. A value reason (derived) is given
    /// once per assessment; a descriptive reason (MRPAAO p.168) may recur, but not
    /// while a draft notice for the assessment is open.
    /// </summary>
    private async Task<Result<(Assessment Assessment, NoticeOfAssessmentItem Item)>> ItemAsync(Guid assessmentId, NoticeReason? requested, CancellationToken ct)
    {
        Result<(Assessment, NoticeOfAssessmentItem)> Failure(string code, string message) => Result.Failure<(Assessment, NoticeOfAssessmentItem)>(code, message);
        var assessment = await db.Assessments.FirstOrDefaultAsync(x => x.Id == assessmentId, ct);
        if (assessment is null)
        {
            return Failure("ASSESSMENT_NOT_FOUND", "No assessment was found with the given id.");
        }
        if (assessment.Status != WorkflowStatus.Posted)
        {
            return Failure("ASSESSMENT_NOT_POSTED", "A notice is given for a posted assessment.");
        }
        var openNotices = db.NoticeOfAssessmentItems.Where(i => i.AssessmentId == assessment.Id)
            .Join(db.NoticesOfAssessment.Where(n => n.Status != NoticeStatus.Cancelled), i => i.NoticeOfAssessmentId, n => n.Id, (i, n) => new { i.Reason, n.Status });
        var descriptive = requested is NoticeReason.DeclaredOwnerChanged or NoticeReason.OwnerAddressChanged or NoticeReason.LocationChanged;
        if (requested is not null && !descriptive)
        {
            return Failure("VALIDATION_FAILED", "A first-assessment, increase or decrease reason is derived from the values; name only a descriptive reason.");
        }
        if (await openNotices.AnyAsync(x => x.Status == NoticeStatus.Draft, ct))
        {
            return Failure("NOTICE_DUPLICATE", "A draft notice for this assessment is already open. Issue or cancel it first.");
        }

        var previous = await AssessmentHistory.PreviousAsync(db, assessment, ct);
        NoticeReason reason;
        if (descriptive)
        {
            reason = requested!.Value;
        }
        else
        {
            if (await openNotices.AnyAsync(x => x.Reason == NoticeReason.FirstAssessment || x.Reason == NoticeReason.AssessmentIncreased
                    || x.Reason == NoticeReason.AssessmentDecreased, ct))
            {
                return Failure("NOTICE_DUPLICATE", "This assessment already has a notice. Cancel it to generate a new one.");
            }
            if (await ValueReasonAsync(assessment, ct) is not { } derived)
            {
                return Failure("NOTICE_NOT_REQUIRED",
                    $"The assessed value is unchanged from the previous assessment ({previous!.AssessedValue:#,0.00}); LGC §223 requires notice only for a first assessment or an increase or decrease. For a change of declared owner, address or location, name that reason.");
            }
            reason = derived;
        }
        var taxDeclaration = await TaxDeclarationLookup.GetCurrentAsync(db, assessment.RpuId, ct);
        return Result.Success((assessment, new NoticeOfAssessmentItem
        {
            PropertyId = assessment.PropertyId, RpuId = assessment.RpuId, AssessmentId = assessment.Id, TaxDeclarationId = taxDeclaration?.Id,
            Reason = reason, PreviousAssessedValue = previous?.AssessedValue, AssessedValue = assessment.AssessedValue,
            MarketValue = assessment.MarketValue, AssessmentYear = assessment.AssessmentYear, AssessmentEffectiveDate = assessment.EffectiveDate,
        }));
    }

    /// <summary>The notice header: the first item's assessment, the items' totals, the addressee, and the §223 period from the earliest approval.</summary>
    private NoticeOfAssessment Header(List<(Assessment Assessment, NoticeOfAssessmentItem Item)> items, string addresseeNames, string? address, Guid? taxpayerId)
    {
        var periods = options.Value;
        var first = items[0].Item;
        var approvedOn = items.Select(x => x.Assessment.ApprovedAt is { } at ? clock.LocalDate(at) : clock.Today).Min();
        for (var i = 0; i < items.Count; i++)
        {
            items[i].Item.Sequence = i + 1;
        }
        return new NoticeOfAssessment
        {
            PropertyId = first.PropertyId, RpuId = first.RpuId, AssessmentId = first.AssessmentId, TaxDeclarationId = first.TaxDeclarationId,
            Reason = first.Reason, PreviousAssessedValue = items.Count == 1 ? first.PreviousAssessedValue : null,
            AssessedValue = items.Sum(x => x.Item.AssessedValue), MarketValue = items.Sum(x => x.Item.MarketValue),
            AssessmentYear = first.AssessmentYear, AssessmentEffectiveDate = first.AssessmentEffectiveDate,
            AddresseeNames = addresseeNames, AddresseeAddress = address, AddresseeTaxpayerId = taxpayerId,
            IssuePeriodDays = periods.IssuePeriodDays!.Value, IssueDueDate = approvedOn.AddDays(periods.IssuePeriodDays.Value),
            AppealPeriodDays = periods.AppealPeriodDays!.Value,
            Items = items.Select(x => x.Item).ToList(),
        };
    }

    public async Task<Result<NoticeDto>> IssueAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var notice = await db.NoticesOfAssessment.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (notice is null)
        {
            return NotFound();
        }
        if (notice.Status != NoticeStatus.Draft)
        {
            return Fail("NOTICE_NOT_DRAFT", "Only a Draft notice can be issued.");
        }
        var ownsTransaction = db.Database.CurrentTransaction is null;
        await using var transaction = ownsTransaction ? await db.Database.BeginTransactionAsync(cancellationToken) : null;
        var context = await NumberContexts.ForPropertyAsync(db, notice.PropertyId, clock.Today.Year, cancellationToken);
        var number = await numbering.GenerateIfConfiguredAsync(NumberedDocumentKind.NoticeOfAssessment, context, clock.Today, cancellationToken);
        if (number.IsFailure)
        {
            return Fail(number.Code!, number.Message!);
        }
        notice.NoticeNumber = number.Value;
        notice.Status = NoticeStatus.Issued;
        notice.IssuedAt = clock.UtcNow;
        notice.IssuedBy = currentUser.AppUserId;
        await db.SaveChangesAsync(cancellationToken);
        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }
        return Result.Success(ToDto(notice));
    }

    public async Task<Result<NoticeDto>> RecordServiceAsync(Guid id, RecordNoticeServiceRequest request, CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(request.ServiceMode) || string.IsNullOrWhiteSpace(request.ServedTo) || request.ServedTo.Length > 300
            || string.IsNullOrWhiteSpace(request.ProofReference) || request.ProofReference.Length > 200 || request.Notes?.Length > 1000
            || request.ReceivedDate == default)
        {
            return Fail("VALIDATION_FAILED", "serviceMode, receivedDate, servedTo (max 300) and proofReference (max 200) are required; notes max 1000.");
        }
        var notice = await db.NoticesOfAssessment.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (notice is null)
        {
            return NotFound();
        }
        if (notice.Status != NoticeStatus.Issued)
        {
            return Fail("NOTICE_NOT_ISSUED", "Service can be recorded only for an issued notice that is not yet served.");
        }
        var issuedOn = clock.LocalDate(notice.IssuedAt!.Value);
        if (request.ReceivedDate < issuedOn || request.ReceivedDate > clock.Today)
        {
            return Fail("NOTICE_RECEIVED_DATE_INVALID", $"The receipt date must be between the issue date ({issuedOn:yyyy-MM-dd}) and today.");
        }
        notice.ServiceMode = request.ServiceMode;
        notice.ReceivedDate = request.ReceivedDate;
        notice.ServedTo = request.ServedTo.Trim();
        notice.ProofReference = request.ProofReference.Trim();
        notice.ServiceNotes = request.Notes;
        notice.ServiceRecordedAt = clock.UtcNow;
        notice.ServiceRecordedBy = currentUser.AppUserId;
        // LGC §226: the appeal period runs from receipt. DOMAIN VERIFICATION REQUIRED: calendar days are
        // counted; whether a deadline on a non-working day moves is not applied.
        notice.AppealDeadline = request.ReceivedDate.AddDays(notice.AppealPeriodDays);
        notice.Status = NoticeStatus.Served;
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success(ToDto(notice));
    }

    public async Task<Result<NoticeDto>> CancelAsync(Guid id, string reason, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > 1000)
        {
            return Fail("VALIDATION_FAILED", "A reason is required (max 1000).");
        }
        var notice = await db.NoticesOfAssessment.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (notice is null)
        {
            return NotFound();
        }
        if (notice.Status is NoticeStatus.Served or NoticeStatus.Cancelled)
        {
            return Fail("NOTICE_NOT_CANCELLABLE", $"A {notice.Status} notice cannot be cancelled.");
        }
        notice.Status = NoticeStatus.Cancelled;
        notice.CancelledAt = clock.UtcNow;
        notice.CancelledBy = currentUser.AppUserId;
        notice.CancellationReason = reason;
        currentUser.Reason = reason;
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success(ToDto(notice));
    }

    public async Task<Result<NoticeDto>> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        await db.NoticesOfAssessment.AsNoTracking().Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == id, cancellationToken) is { } notice
            ? Result.Success(ToDto(notice))
            : NotFound();

    public async Task<Result<IReadOnlyList<NoticeDto>>> ListByPropertyAsync(Guid propertyId, CancellationToken cancellationToken = default) =>
        // A combined notice shows on every property it lists.
        Result.Success<IReadOnlyList<NoticeDto>>((await db.NoticesOfAssessment.AsNoTracking().Include(x => x.Items)
            .Where(x => x.PropertyId == propertyId || x.Items.Any(i => i.PropertyId == propertyId))
            .OrderByDescending(x => x.CreatedAt).ToListAsync(cancellationToken)).Select(ToDto).ToList());

    private NoticeDto ToDto(NoticeOfAssessment x) => new(
        x.Id, x.NoticeNumber, x.PropertyId, x.RpuId, x.AssessmentId, x.TaxDeclarationId, x.Reason, x.PreviousAssessedValue, x.AssessedValue,
        x.MarketValue, x.AssessmentYear, x.AssessmentEffectiveDate, x.AddresseeNames, x.AddresseeAddress, x.IssuePeriodDays, x.IssueDueDate,
        x.Status == NoticeStatus.Draft && clock.Today > x.IssueDueDate, x.AppealPeriodDays, x.Status, x.CreatedAt, x.IssuedAt,
        x.ServiceMode, x.ReceivedDate, x.ServedTo, x.ProofReference, x.ServiceNotes, x.AppealDeadline, x.CancelledAt, x.CancellationReason,
        x.AddresseeTaxpayerId,
        x.Items.OrderBy(i => i.Sequence).Select(i => new NoticeItemDto(i.Sequence, i.PropertyId, i.RpuId, i.AssessmentId, i.TaxDeclarationId,
            i.Reason, i.PreviousAssessedValue, i.AssessedValue, i.MarketValue, i.AssessmentYear, i.AssessmentEffectiveDate)).ToList());

    private static Result<NoticeDto> NotFound() => Fail("NOTICE_NOT_FOUND", "No Notice of Assessment was found with the given id.");

    private static Result<NoticeDto> Fail(string code, string message) => Result.Failure<NoticeDto>(code, message);
}
