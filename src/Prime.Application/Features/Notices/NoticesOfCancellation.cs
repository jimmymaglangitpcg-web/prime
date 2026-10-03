using Microsoft.EntityFrameworkCore;
using Prime.Application.Common;
using Prime.Application.Common.Interfaces;
using Prime.Application.Features.Forms;
using Prime.Application.Features.Numbering;
using Prime.Application.Features.Properties;
using Prime.Domain.Entities;
using Prime.Domain.Entities.Notices;
using Prime.Domain.Enums;

namespace Prime.Application.Features.Notices;

public sealed record NoticeOfCancellationDto(
    Guid Id, string? NoticeNumber, Guid PropertyId, string Pin, Guid TaxDeclarationId, string TaxDeclarationNumber,
    Guid? ReplacedByTaxDeclarationId, string? ReplacedByTaxDeclarationNumber, Guid? PropertyTransactionId,
    CancellationNoticeGround Ground, string Reason, DateOnly CancelledOn, string AddresseeNames, string? AddresseeAddress,
    NoticeStatus Status, DateTimeOffset CreatedAt, DateTimeOffset? IssuedAt,
    NoticeServiceMode? ServiceMode, DateOnly? ReceivedDate, string? ServedTo, string? EmailAddress, DateOnly? SentDate,
    string? ProofReference, string? ServiceNotes, DateTimeOffset? CancelledAt, string? CancellationReason);

public interface INoticeOfCancellationService
{
    Task<Result<IReadOnlyList<NoticeOfCancellationDto>>> ListByPropertyAsync(Guid propertyId, CancellationToken cancellationToken = default);
    Task<Result<NoticeOfCancellationDto>> GetAsync(Guid id, CancellationToken cancellationToken = default);
    /// <summary>Marks the notice issued and numbers it (NoticeOfCancellation scheme, if any).</summary>
    Task<Result<NoticeOfCancellationDto>> IssueAsync(Guid id, CancellationToken cancellationToken = default);
    /// <summary>Records service as for the Notice of Assessment (mode, receipt, proof).</summary>
    Task<Result<NoticeOfCancellationDto>> RecordServiceAsync(Guid id, RecordNoticeServiceRequest request, CancellationToken cancellationToken = default);
    Task<Result<NoticeOfCancellationDto>> CancelAsync(Guid id, string reason, CancellationToken cancellationToken = default);
}

/// <summary>
/// Notices of Cancellation (LAM 2025 Book III p.89 C; docs/analysis/assessment-listing-exemptions.md §4.4, Q11):
/// generated as Drafts when a cancellation is approved (<see cref="GenerateAsync"/>), then issued and served.
/// </summary>
public sealed class NoticeOfCancellationService(IApplicationDbContext db, ICurrentUserService currentUser, INumberingService numbering, IClock clock)
    : INoticeOfCancellationService
{
    /// <summary>
    /// Drafts notices for the cancelled <paramref name="cancelled"/>: one per addressee address, to
    /// <paramref name="addressees"/> (declarants and parties with a legal interest). A notice already drafted for the same
    /// TD, ground and address is not repeated. The caller saves.
    /// </summary>
    internal static async Task GenerateAsync(IApplicationDbContext db, TaxDeclaration cancelled, TaxDeclaration? replacedBy, Guid? transactionId,
        CancellationNoticeGround ground, string reason, DateOnly cancelledOn, IEnumerable<PropertyOwnerDto> addressees, CancellationToken ct)
    {
        var existing = await db.NoticesOfCancellation.AsNoTracking()
            .Where(x => x.TaxDeclarationId == cancelled.Id && x.Ground == ground && x.Status != NoticeStatus.Cancelled)
            .Select(x => x.AddresseeAddress).ToListAsync(ct);
        var pending = db.NoticesOfCancellation.Local.Where(x => x.TaxDeclarationId == cancelled.Id && x.Ground == ground).Select(x => x.AddresseeAddress);
        var done = existing.Concat(pending).Select(Key).ToHashSet();
        foreach (var group in addressees.Where(p => p.Role != PropertyPartyRole.UnknownOwner)
                     .GroupBy(p => Key(p.Address)).Where(g => !done.Contains(g.Key)))
        {
            var parties = group.ToList();
            db.NoticesOfCancellation.Add(new NoticeOfCancellation
            {
                PropertyId = cancelled.PropertyId, TaxDeclarationId = cancelled.Id, TaxDeclarationNumber = cancelled.TaxDeclarationNumber,
                ReplacedByTaxDeclarationId = replacedBy?.Id, ReplacedByTaxDeclarationNumber = replacedBy?.TaxDeclarationNumber,
                PropertyTransactionId = transactionId, Ground = ground, Reason = Trim(reason, 1000), CancelledOn = cancelledOn,
                AddresseeNames = Trim(string.Join("; ", parties.Select(p => p.Role is PropertyPartyRole.Owner
                    ? p.TaxpayerDisplayName : $"{p.TaxpayerDisplayName} ({PropertyParties.RoleLabel(p.Role)})").Distinct()), 2000),
                AddresseeAddress = parties[0].Address,
            });
        }
    }

    /// <summary>
    /// The previous-owner case (LAM Book I §1 g, as recorded in the design): a TD declaring a new assessment replaces
    /// one declared in the name of owners who no longer own the unit. Those owners get a notice. Called on approval;
    /// the caller saves.
    /// </summary>
    internal static async Task PreviousOwnersAsync(IApplicationDbContext db, TaxDeclaration replaced, TaxDeclaration replacing, DateOnly on,
        CancellationToken ct)
    {
        if (replacing.AssessmentId is null || replacing.AssessmentId == replaced.AssessmentId)
        {
            return; // the same assessment declared again (a transfer): no assessment of the previous owner is cancelled
        }
        var eff = replaced.EffectivityDate;
        var declared = await PropertyParties.ProjectAsync(await PropertyParties.ScopeAsync(db, replaced.PropertyId, replaced.RpuId,
            x => x.StartDate <= eff && (x.EndDate == null || x.EndDate >= eff) && x.Role == PropertyPartyRole.Owner, ct), ct);
        var current = (await PropertyParties.ProjectAsync(await PropertyParties.ScopeAsync(db, replacing.PropertyId, replacing.RpuId,
            x => x.IsCurrent && x.Role == PropertyPartyRole.Owner, ct), ct)).Select(p => p.TaxpayerId).ToHashSet();
        var previous = declared.Where(p => p.TaxpayerId is { } id && !current.Contains(id)).ToList();
        if (previous.Count > 0)
        {
            await GenerateAsync(db, replaced, replacing, replacing.PropertyTransactionId, CancellationNoticeGround.PreviousOwner,
                $"The assessment declared in TD {replaced.TaxDeclarationNumber} was cancelled by the reassessment declared in TD {replacing.TaxDeclarationNumber}.",
                on, previous, ct);
        }
    }

    public async Task<Result<IReadOnlyList<NoticeOfCancellationDto>>> ListByPropertyAsync(Guid propertyId, CancellationToken cancellationToken = default) =>
        Result.Success<IReadOnlyList<NoticeOfCancellationDto>>(await Query().Where(x => x.PropertyId == propertyId)
            .OrderByDescending(x => x.CreatedAt).Select(Projection).ToListAsync(cancellationToken));

    public async Task<Result<NoticeOfCancellationDto>> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        await Query().Where(x => x.Id == id).Select(Projection).FirstOrDefaultAsync(cancellationToken) is { } dto ? Result.Success(dto) : NotFound();

    public async Task<Result<NoticeOfCancellationDto>> IssueAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var notice = await db.NoticesOfCancellation.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
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
        var context = await NumberContexts.ForPropertyAsync(db, notice.PropertyId, clock.Today.Year, cancellationToken, clock.Today);
        var number = await numbering.GenerateIfConfiguredAsync(NumberedDocumentKind.NoticeOfCancellation, context, clock.Today, cancellationToken);
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
        return await GetAsync(id, cancellationToken);
    }

    public async Task<Result<NoticeOfCancellationDto>> RecordServiceAsync(Guid id, RecordNoticeServiceRequest request, CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(request.ServiceMode) || string.IsNullOrWhiteSpace(request.ServedTo) || request.ServedTo.Length > 300
            || string.IsNullOrWhiteSpace(request.ProofReference) || request.ProofReference.Length > 200 || request.Notes?.Length > 1000
            || request.ReceivedDate == default)
        {
            return Fail("VALIDATION_FAILED", "serviceMode, receivedDate, servedTo (max 300) and proofReference (max 200) are required; notes max 1000.");
        }
        if (request.ServiceMode == NoticeServiceMode.Email && string.IsNullOrWhiteSpace(request.EmailAddress))
        {
            return Fail("VALIDATION_FAILED", "An emailed notice needs the address it was sent to.");
        }
        var notice = await db.NoticesOfCancellation.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (notice is null)
        {
            return NotFound();
        }
        if (notice.Status != NoticeStatus.Issued)
        {
            return Fail("NOTICE_NOT_ISSUED", "Service can be recorded only for an issued notice that is not yet served.");
        }
        var issuedOn = clock.LocalDate(notice.IssuedAt!.Value);
        if (request.ReceivedDate < issuedOn || request.ReceivedDate > clock.Today || request.SentDate > request.ReceivedDate || request.SentDate < issuedOn)
        {
            return Fail("VALIDATION_FAILED", "The notice is sent and received between its issue and today, the sending first.");
        }
        notice.ServiceMode = request.ServiceMode;
        notice.ReceivedDate = request.ReceivedDate;
        notice.ServedTo = request.ServedTo.Trim();
        notice.ProofReference = request.ProofReference.Trim();
        notice.ServiceNotes = request.Notes;
        notice.EmailAddress = request.ServiceMode == NoticeServiceMode.Email ? request.EmailAddress!.Trim() : null;
        notice.SentDate = request.SentDate;
        notice.ServiceRecordedAt = clock.UtcNow;
        notice.ServiceRecordedBy = currentUser.AppUserId;
        notice.Status = NoticeStatus.Served;
        await db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    public async Task<Result<NoticeOfCancellationDto>> CancelAsync(Guid id, string reason, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > 1000)
        {
            return Fail("VALIDATION_FAILED", "A reason is required (max 1000).");
        }
        var notice = await db.NoticesOfCancellation.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (notice is null)
        {
            return NotFound();
        }
        if (notice.Status is NoticeStatus.Cancelled or NoticeStatus.Served)
        {
            return Fail("NOTICE_NOT_CANCELLABLE", $"A {notice.Status} notice cannot be cancelled.");
        }
        notice.Status = NoticeStatus.Cancelled;
        notice.CancelledAt = clock.UtcNow;
        notice.CancelledBy = currentUser.AppUserId;
        notice.CancellationReason = reason.Trim();
        currentUser.Reason = reason;
        await db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    private IQueryable<NoticeOfCancellation> Query() => db.NoticesOfCancellation.AsNoTracking();

    private static readonly System.Linq.Expressions.Expression<Func<NoticeOfCancellation, NoticeOfCancellationDto>> Projection = x => new NoticeOfCancellationDto(
        x.Id, x.NoticeNumber, x.PropertyId, x.Property!.PropertyIdentificationNumber, x.TaxDeclarationId, x.TaxDeclarationNumber,
        x.ReplacedByTaxDeclarationId, x.ReplacedByTaxDeclarationNumber, x.PropertyTransactionId, x.Ground, x.Reason, x.CancelledOn,
        x.AddresseeNames, x.AddresseeAddress, x.Status, x.CreatedAt, x.IssuedAt, x.ServiceMode, x.ReceivedDate, x.ServedTo, x.EmailAddress, x.SentDate,
        x.ProofReference, x.ServiceNotes, x.CancelledAt, x.CancellationReason);

    private static string Key(string? address) => (address ?? string.Empty).Trim().ToUpperInvariant();

    private static string Trim(string value, int max) => value.Length <= max ? value : value[..max];

    private static Result<NoticeOfCancellationDto> Fail(string code, string message) => Result.Failure<NoticeOfCancellationDto>(code, message);

    private static Result<NoticeOfCancellationDto> NotFound() => Fail("NOTICE_OF_CANCELLATION_NOT_FOUND", "No Notice of Cancellation was found with the given id.");
}

/// <summary>The Notice of Cancellation form's data; the subject is the notice.</summary>
public sealed class NoticeOfCancellationFormDataProvider(IApplicationDbContext db, INoticeOfCancellationService notices) : IFormDataProvider
{
    public FormSubjectType SubjectType => FormSubjectType.NoticeOfCancellation;

    public async Task<FormSubjectData?> BuildAsync(Guid subjectId, CancellationToken cancellationToken)
    {
        var result = await notices.GetAsync(subjectId, cancellationToken);
        if (result.IsFailure)
        {
            return null;
        }
        var n = result.Value;
        var data = FormData.ToJson(new
        {
            notice = n,
            property = await FormData.PropertyAsync(db, n.PropertyId, cancellationToken),
        });
        var blocker = n.Status switch
        {
            NoticeStatus.Draft => "Issue the notice first; preview it meanwhile.",
            NoticeStatus.Cancelled => "A cancelled notice cannot be issued.",
            _ => null,
        };
        return new FormSubjectData(n.NoticeNumber, data, blocker);
    }
}
