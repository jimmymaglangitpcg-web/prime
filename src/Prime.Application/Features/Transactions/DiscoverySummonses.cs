using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Prime.Application.Common;
using Prime.Application.Common.Interfaces;
using Prime.Application.Features.Forms;
using Prime.Application.Features.Notices;
using Prime.Application.Features.Numbering;
using Prime.Domain.Entities.Transactions;
using Prime.Domain.Enums;

namespace Prime.Application.Features.Transactions;

/// <summary>"Discovery" configuration section (docs/analysis/assessment-listing-exemptions.md Q10).</summary>
public sealed class DiscoveryOptions
{
    public const string SectionName = "Discovery";

    /// <summary>Days after receipt to comply with a summons; the LAM gives 15, and 15 more for a second (Book III p.85–86).</summary>
    public int SummonsPeriodDays { get; set; } = 15;

    /// <summary>Printed on the summons.</summary>
    public string SummonsLegalBasis { get; set; } = "LGC §213; LAM 2025 Book III pp.85–86 (discovery)";
}

public sealed record IssueSummonsRequest(string AddresseeName, string? AddresseeAddress, Guid? AddresseeTaxpayerId, DateOnly? IssuedOn);

public sealed record RecordSummonsServiceRequest(NoticeServiceMode ServiceMode, DateOnly ReceivedOn, string ServedTo, string ProofReference, string? Notes);

public sealed record SummonsOutcomeRequest(SummonsOutcome Outcome, DateOnly OutcomeOn, string? Notes);

public sealed record VerificationRequest(string Note);

/// <param name="Overdue">Served, still pending, and past its due date.</param>
public sealed record DiscoverySummonsDto(
    Guid Id, Guid PropertyTransactionId, int Sequence, string? SummonsNumber, string AddresseeName, string? AddresseeAddress, Guid? AddresseeTaxpayerId,
    DateOnly IssuedOn, int PeriodDays, NoticeServiceMode? ServiceMode, DateOnly? ReceivedOn, string? ServedTo, string? ProofReference, string? ServiceNotes,
    DateOnly? DueDate, bool Overdue, SummonsOutcome Outcome, DateOnly? OutcomeOn, string? OutcomeNotes, DateTimeOffset CreatedAt);

public sealed record DiscoveryDto(IReadOnlyList<DiscoverySummonsDto> Summonses, string? InterAgencyVerification, DateOnly? VerificationRecordedOn,
    bool CanIssue, string? NextStep);

public interface IDiscoverySummonsService
{
    Task<Result<DiscoveryDto>> GetAsync(Guid transactionId, CancellationToken cancellationToken = default);
    Task<Result<DiscoveryDto>> IssueAsync(Guid transactionId, IssueSummonsRequest request, CancellationToken cancellationToken = default);
    Task<Result<DiscoveryDto>> RecordServiceAsync(Guid summonsId, RecordSummonsServiceRequest request, CancellationToken cancellationToken = default);
    Task<Result<DiscoveryDto>> RecordOutcomeAsync(Guid summonsId, SummonsOutcomeRequest request, CancellationToken cancellationToken = default);
    Task<Result<DiscoveryDto>> RecordVerificationAsync(Guid transactionId, VerificationRequest request, CancellationToken cancellationToken = default);
    Task<Result<DiscoverySummonsDto>> GetSummonsAsync(Guid summonsId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Summonses on a new-discovery transaction (LAM 2025 Book III pp.85–86; docs/analysis/assessment-listing-exemptions.md
/// §4.4, Q10): a first summons; a second only after the first was not complied with; after an unanswered second, the
/// verification with other agencies, recorded on the transaction, before the assessor declares the property. Summonses
/// never block the transaction: the assessor may declare the property under LGC §204 regardless.
/// </summary>
public sealed class DiscoverySummonsService(IApplicationDbContext db, INumberingService numbering, IClock clock, IOptions<DiscoveryOptions> options)
    : IDiscoverySummonsService
{
    public async Task<Result<DiscoveryDto>> GetAsync(Guid transactionId, CancellationToken cancellationToken = default)
    {
        var tx = await db.PropertyTransactions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == transactionId, cancellationToken);
        return tx is null ? Fail("PROPERTY_TRANSACTION_NOT_FOUND", "No property transaction was found with the given id.") : Result.Success(await MapAsync(tx, cancellationToken));
    }

    public async Task<Result<DiscoveryDto>> IssueAsync(Guid transactionId, IssueSummonsRequest r, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(r.AddresseeName) || r.AddresseeName.Length > 300 || r.AddresseeAddress?.Length > 1000)
        {
            return Fail("VALIDATION_FAILED", "The addressee's name is required (max 300); address max 1000.");
        }
        var tx = await db.PropertyTransactions.FirstOrDefaultAsync(x => x.Id == transactionId, cancellationToken);
        if (tx is null)
        {
            return Fail("PROPERTY_TRANSACTION_NOT_FOUND", "No property transaction was found with the given id.");
        }
        if (tx.Kind != PropertyTransactionKind.NewDiscovery || tx.Status is not (WorkflowStatus.Draft or WorkflowStatus.PendingReview))
        {
            return Fail("SUMMONS_NOT_ALLOWED", "Summonses are issued on an open new-discovery transaction.");
        }
        var issued = await db.DiscoverySummonses.Where(x => x.PropertyTransactionId == tx.Id).OrderBy(x => x.Sequence).ToListAsync(cancellationToken);
        if (CannotIssue(issued) is { } why)
        {
            return Fail("SUMMONS_NOT_ALLOWED", why);
        }
        if (r.AddresseeTaxpayerId is { } tp && !await db.Taxpayers.AnyAsync(x => x.Id == tp, cancellationToken))
        {
            return Fail("TAXPAYER_NOT_FOUND", "The specified addressee does not exist.");
        }
        var issuedOn = r.IssuedOn ?? clock.Today;
        if (issuedOn > clock.Today || (issued.LastOrDefault() is { } first && issuedOn < first.OutcomeOn))
        {
            return Fail("VALIDATION_FAILED", "A summons is issued on or before today, and a second one after the first was found not complied with.");
        }
        var ownsTransaction = db.Database.CurrentTransaction is null;
        await using var transaction = ownsTransaction ? await db.Database.BeginTransactionAsync(cancellationToken) : null;
        var context = await NumberContexts.ForPropertyAsync(db, tx.PropertyId, issuedOn.Year, cancellationToken, clock.Today);
        var number = await numbering.GenerateIfConfiguredAsync(NumberedDocumentKind.DiscoverySummons, context, clock.Today, cancellationToken);
        if (number.IsFailure)
        {
            return Fail(number.Code!, number.Message!);
        }
        db.DiscoverySummonses.Add(new DiscoverySummons
        {
            PropertyTransactionId = tx.Id, Sequence = issued.Count + 1, SummonsNumber = number.Value, AddresseeName = r.AddresseeName.Trim(),
            AddresseeAddress = Clean(r.AddresseeAddress), AddresseeTaxpayerId = r.AddresseeTaxpayerId, IssuedOn = issuedOn,
            PeriodDays = options.Value.SummonsPeriodDays,
        });
        await db.SaveChangesAsync(cancellationToken);
        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }
        return Result.Success(await MapAsync(tx, cancellationToken));
    }

    public async Task<Result<DiscoveryDto>> RecordServiceAsync(Guid summonsId, RecordSummonsServiceRequest r, CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(r.ServiceMode) || string.IsNullOrWhiteSpace(r.ServedTo) || r.ServedTo.Length > 300
            || string.IsNullOrWhiteSpace(r.ProofReference) || r.ProofReference.Length > 200 || r.Notes?.Length > 1000 || r.ReceivedOn == default)
        {
            return Fail("VALIDATION_FAILED", "serviceMode, receivedOn, servedTo (max 300) and proofReference (max 200) are required; notes max 1000.");
        }
        var s = await db.DiscoverySummonses.Include(x => x.PropertyTransaction).FirstOrDefaultAsync(x => x.Id == summonsId, cancellationToken);
        if (s is null)
        {
            return NotFound();
        }
        if (s.ReceivedOn is not null)
        {
            return Fail("SUMMONS_ALREADY_SERVED", "Service of this summons is already recorded.");
        }
        if (r.ReceivedOn < s.IssuedOn || r.ReceivedOn > clock.Today)
        {
            return Fail("VALIDATION_FAILED", "The summons is received between its issue and today.");
        }
        (s.ServiceMode, s.ReceivedOn, s.ServedTo, s.ProofReference, s.ServiceNotes) = (r.ServiceMode, r.ReceivedOn, r.ServedTo.Trim(), r.ProofReference.Trim(), Clean(r.Notes));
        s.DueDate = r.ReceivedOn.AddDays(s.PeriodDays);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success(await MapAsync(s.PropertyTransaction!, cancellationToken));
    }

    public async Task<Result<DiscoveryDto>> RecordOutcomeAsync(Guid summonsId, SummonsOutcomeRequest r, CancellationToken cancellationToken = default)
    {
        if (r.Outcome == SummonsOutcome.Pending || !Enum.IsDefined(r.Outcome) || r.OutcomeOn == default || r.Notes?.Length > 1000)
        {
            return Fail("VALIDATION_FAILED", "The outcome (Complied or NotComplied) and its date are required; notes max 1000.");
        }
        var s = await db.DiscoverySummonses.Include(x => x.PropertyTransaction).FirstOrDefaultAsync(x => x.Id == summonsId, cancellationToken);
        if (s is null)
        {
            return NotFound();
        }
        if (s.Outcome != SummonsOutcome.Pending)
        {
            return Fail("SUMMONS_CLOSED", $"This summons is already closed ({s.Outcome}).");
        }
        if (s.ReceivedOn is null)
        {
            return Fail("SUMMONS_NOT_SERVED", "Record the summons's service first: its period runs from receipt.");
        }
        if (r.OutcomeOn < s.ReceivedOn || r.OutcomeOn > clock.Today)
        {
            return Fail("VALIDATION_FAILED", "The outcome is dated between the receipt and today.");
        }
        // Not complied with only once the period has run out.
        if (r.Outcome == SummonsOutcome.NotComplied && r.OutcomeOn <= s.DueDate)
        {
            return Fail("SUMMONS_PERIOD_RUNNING", $"The owner has until {s.DueDate:yyyy-MM-dd} to comply; record non-compliance after that.");
        }
        (s.Outcome, s.OutcomeOn, s.OutcomeNotes) = (r.Outcome, r.OutcomeOn, Clean(r.Notes));
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success(await MapAsync(s.PropertyTransaction!, cancellationToken));
    }

    public async Task<Result<DiscoveryDto>> RecordVerificationAsync(Guid transactionId, VerificationRequest r, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(r.Note) || r.Note.Length > 2000)
        {
            return Fail("VALIDATION_FAILED", "A note of the verification is required (max 2000).");
        }
        var tx = await db.PropertyTransactions.FirstOrDefaultAsync(x => x.Id == transactionId, cancellationToken);
        if (tx is null)
        {
            return Fail("PROPERTY_TRANSACTION_NOT_FOUND", "No property transaction was found with the given id.");
        }
        if (tx.Status is not (WorkflowStatus.Draft or WorkflowStatus.PendingReview))
        {
            return Fail("PROPERTY_TRANSACTION_CLOSED", $"A {tx.Status} transaction cannot be changed.");
        }
        if (!await db.DiscoverySummonses.AnyAsync(x => x.PropertyTransactionId == tx.Id && x.Sequence == 2 && x.Outcome == SummonsOutcome.NotComplied, cancellationToken))
        {
            return Fail("VERIFICATION_NOT_DUE", "The verification with other agencies follows a second summons not complied with.");
        }
        tx.InterAgencyVerification = r.Note.Trim();
        tx.VerificationRecordedOn = clock.Today;
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success(await MapAsync(tx, cancellationToken));
    }

    public async Task<Result<DiscoverySummonsDto>> GetSummonsAsync(Guid summonsId, CancellationToken cancellationToken = default) =>
        await db.DiscoverySummonses.AsNoTracking().FirstOrDefaultAsync(x => x.Id == summonsId, cancellationToken) is { } s
            ? Result.Success(ToDto(s))
            : Result.Failure<DiscoverySummonsDto>("SUMMONS_NOT_FOUND", "No summons was found with the given id.");

    /// <summary>Why another summons cannot be issued, or null.</summary>
    private static string? CannotIssue(List<DiscoverySummons> issued) => issued.Count switch
    {
        0 => null,
        1 when issued[0].Outcome == SummonsOutcome.NotComplied => null,
        1 when issued[0].Outcome == SummonsOutcome.Complied => "The owner complied with the first summons.",
        1 => "A second summons follows only a first one found not complied with after its period.",
        _ => "Two summonses have been issued; after the second, record the verification with other agencies.",
    };

    private async Task<DiscoveryDto> MapAsync(PropertyTransaction tx, CancellationToken ct)
    {
        var summonses = await db.DiscoverySummonses.AsNoTracking().Where(x => x.PropertyTransactionId == tx.Id).OrderBy(x => x.Sequence).ToListAsync(ct);
        var open = tx.Status is WorkflowStatus.Draft or WorkflowStatus.PendingReview;
        var canIssue = open && tx.Kind == PropertyTransactionKind.NewDiscovery && CannotIssue(summonses) is null;
        var last = summonses.LastOrDefault();
        var next = tx.Kind != PropertyTransactionKind.NewDiscovery ? null
            : last is null ? "Issue the first summons to the owner."
            : last.ReceivedOn is null ? $"Record the service of summons {last.Sequence}."
            : last.Outcome == SummonsOutcome.Pending ? $"Due {last.DueDate:yyyy-MM-dd}: record whether the owner complied."
            : last.Outcome == SummonsOutcome.Complied ? "The owner complied: proceed with the declaration."
            : last.Sequence == 1 ? "Not complied with: issue the second summons."
            : tx.InterAgencyVerification is null ? "Second summons not complied with: record the verification with other agencies, then declare the property (LGC §204)."
            : "Verification recorded: the assessor declares the property (LGC §204).";
        return new DiscoveryDto(summonses.Select(ToDto).ToList(), tx.InterAgencyVerification, tx.VerificationRecordedOn, canIssue, next);
    }

    private DiscoverySummonsDto ToDto(DiscoverySummons s) => new(
        s.Id, s.PropertyTransactionId, s.Sequence, s.SummonsNumber, s.AddresseeName, s.AddresseeAddress, s.AddresseeTaxpayerId, s.IssuedOn, s.PeriodDays,
        s.ServiceMode, s.ReceivedOn, s.ServedTo, s.ProofReference, s.ServiceNotes, s.DueDate,
        s.Outcome == SummonsOutcome.Pending && s.DueDate < clock.Today, s.Outcome, s.OutcomeOn, s.OutcomeNotes, s.CreatedAt);

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static Result<DiscoveryDto> Fail(string code, string message) => Result.Failure<DiscoveryDto>(code, message);

    private static Result<DiscoveryDto> NotFound() => Fail("SUMMONS_NOT_FOUND", "No summons was found with the given id.");
}

/// <summary>The discovery summons form's data; the subject is the summons.</summary>
public sealed class DiscoverySummonsFormDataProvider(IApplicationDbContext db, IDiscoverySummonsService summonses, IOptions<DiscoveryOptions> options)
    : IFormDataProvider
{
    public FormSubjectType SubjectType => FormSubjectType.DiscoverySummons;

    public async Task<FormSubjectData?> BuildAsync(Guid subjectId, CancellationToken cancellationToken)
    {
        var result = await summonses.GetSummonsAsync(subjectId, cancellationToken);
        if (result.IsFailure)
        {
            return null;
        }
        var s = result.Value;
        var tx = await db.PropertyTransactions.AsNoTracking().Where(x => x.Id == s.PropertyTransactionId)
            .Select(x => new { x.PropertyId, x.TransactionNumber, x.TypeCode, x.Description }).FirstAsync(cancellationToken);
        var data = FormData.ToJson(new
        {
            summons = s,
            transaction = new { number = tx.TransactionNumber, code = tx.TypeCode, description = tx.Description },
            property = await FormData.PropertyAsync(db, tx.PropertyId, cancellationToken),
            legalBasis = options.Value.SummonsLegalBasis,
        });
        return new FormSubjectData(s.SummonsNumber, data, null);
    }
}
