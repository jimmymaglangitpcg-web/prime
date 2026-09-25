using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Prime.Application.Common;
using Prime.Application.Common.Interfaces;
using Prime.Application.Features.Numbering;
using Prime.Application.Features.Properties;
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

public sealed record GenerateNoticeRequest(Guid AssessmentId);

public sealed record RecordNoticeServiceRequest(NoticeServiceMode ServiceMode, DateOnly ReceivedDate, string ServedTo, string ProofReference, string? Notes);

public sealed record NoticeReasonRequest(string Reason);

public sealed record NoticeDto(
    Guid Id, string? NoticeNumber, Guid PropertyId, Guid RpuId, Guid AssessmentId, Guid? TaxDeclarationId,
    NoticeReason Reason, decimal? PreviousAssessedValue, decimal AssessedValue, decimal MarketValue,
    int AssessmentYear, DateOnly AssessmentEffectiveDate, string AddresseeNames, string? AddresseeAddress,
    int IssuePeriodDays, DateOnly IssueDueDate, bool IssueOverdue, int AppealPeriodDays,
    NoticeStatus Status, DateTimeOffset CreatedAt, DateTimeOffset? IssuedAt,
    NoticeServiceMode? ServiceMode, DateOnly? ReceivedDate, string? ServedTo, string? ProofReference, string? ServiceNotes,
    DateOnly? AppealDeadline, DateTimeOffset? CancelledAt, string? CancellationReason);

public interface INoticeService
{
    /// <summary>
    /// A Draft notice for a posted assessment, when LGC §223 requires one: a
    /// first assessment, or an increase or decrease against the previous assessment.
    /// </summary>
    Task<Result<NoticeDto>> GenerateAsync(GenerateNoticeRequest request, CancellationToken cancellationToken = default);
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
        var assessment = await db.Assessments.FirstOrDefaultAsync(x => x.Id == request.AssessmentId, cancellationToken);
        if (assessment is null)
        {
            return Fail("ASSESSMENT_NOT_FOUND", "No assessment was found with the given id.");
        }
        if (assessment.Status != WorkflowStatus.Posted)
        {
            return Fail("ASSESSMENT_NOT_POSTED", "A notice is given for a posted assessment.");
        }
        if (await db.NoticesOfAssessment.AnyAsync(x => x.AssessmentId == assessment.Id && x.Status != NoticeStatus.Cancelled, cancellationToken))
        {
            return Fail("NOTICE_DUPLICATE", "This assessment already has a notice. Cancel it to generate a new one.");
        }

        var previous = await AssessmentHistory.PreviousAsync(db, assessment, cancellationToken);
        NoticeReason reason;
        if (previous is null)
        {
            reason = NoticeReason.FirstAssessment;
        }
        else if (assessment.AssessedValue > previous.AssessedValue)
        {
            reason = NoticeReason.AssessmentIncreased;
        }
        else if (assessment.AssessedValue < previous.AssessedValue)
        {
            reason = NoticeReason.AssessmentDecreased;
        }
        else
        {
            return Fail("NOTICE_NOT_REQUIRED",
                $"The assessed value is unchanged from the previous assessment ({previous.AssessedValue:#,0.00}); LGC §223 requires notice only for a first assessment or an increase or decrease.");
        }

        var parties = await PropertyParties.ProjectAsync(
            (await PropertyParties.ScopeAsync(db, assessment.PropertyId, assessment.RpuId, x => x.IsCurrent, cancellationToken))
                .Where(x => x.Role != PropertyPartyRole.LegalInterestHolder), cancellationToken);
        if (parties.Count == 0)
        {
            return Fail("NOTICE_NO_ADDRESSEE",
                "The property has no current declared party (owner, administrator, beneficial user, claimant or unknown owner) to address the notice to.");
        }
        var taxDeclaration = await TaxDeclarationLookup.GetCurrentAsync(db, assessment.RpuId, cancellationToken);
        var periods = options.Value;
        var approvedOn = assessment.ApprovedAt is { } approvedAt ? clock.LocalDate(approvedAt) : clock.Today;

        var notice = new NoticeOfAssessment
        {
            PropertyId = assessment.PropertyId, RpuId = assessment.RpuId, AssessmentId = assessment.Id, TaxDeclarationId = taxDeclaration?.Id,
            Reason = reason, PreviousAssessedValue = previous?.AssessedValue, AssessedValue = assessment.AssessedValue,
            MarketValue = assessment.MarketValue, AssessmentYear = assessment.AssessmentYear, AssessmentEffectiveDate = assessment.EffectiveDate,
            AddresseeNames = string.Join("; ", parties.Select(p => p.Role is PropertyPartyRole.Owner or PropertyPartyRole.UnknownOwner
                ? p.TaxpayerDisplayName
                : $"{p.TaxpayerDisplayName} ({PropertyParties.RoleLabel(p.Role)})")),
            AddresseeAddress = parties.Select(p => p.Address).FirstOrDefault(a => !string.IsNullOrWhiteSpace(a)),
            IssuePeriodDays = periods.IssuePeriodDays!.Value, IssueDueDate = approvedOn.AddDays(periods.IssuePeriodDays.Value),
            AppealPeriodDays = periods.AppealPeriodDays!.Value,
        };
        db.NoticesOfAssessment.Add(notice);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success(ToDto(notice));
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
        await db.NoticesOfAssessment.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken) is { } notice
            ? Result.Success(ToDto(notice))
            : NotFound();

    public async Task<Result<IReadOnlyList<NoticeDto>>> ListByPropertyAsync(Guid propertyId, CancellationToken cancellationToken = default) =>
        Result.Success<IReadOnlyList<NoticeDto>>((await db.NoticesOfAssessment.AsNoTracking().Where(x => x.PropertyId == propertyId)
            .OrderByDescending(x => x.CreatedAt).ToListAsync(cancellationToken)).Select(ToDto).ToList());

    private NoticeDto ToDto(NoticeOfAssessment x) => new(
        x.Id, x.NoticeNumber, x.PropertyId, x.RpuId, x.AssessmentId, x.TaxDeclarationId, x.Reason, x.PreviousAssessedValue, x.AssessedValue,
        x.MarketValue, x.AssessmentYear, x.AssessmentEffectiveDate, x.AddresseeNames, x.AddresseeAddress, x.IssuePeriodDays, x.IssueDueDate,
        x.Status == NoticeStatus.Draft && clock.Today > x.IssueDueDate, x.AppealPeriodDays, x.Status, x.CreatedAt, x.IssuedAt,
        x.ServiceMode, x.ReceivedDate, x.ServedTo, x.ProofReference, x.ServiceNotes, x.AppealDeadline, x.CancelledAt, x.CancellationReason);

    private static Result<NoticeDto> NotFound() => Fail("NOTICE_NOT_FOUND", "No Notice of Assessment was found with the given id.");

    private static Result<NoticeDto> Fail(string code, string message) => Result.Failure<NoticeDto>(code, message);
}
