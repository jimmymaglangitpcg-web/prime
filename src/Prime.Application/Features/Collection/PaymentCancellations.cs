using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Prime.Application.Common;
using Prime.Application.Features.Numbering;
using Prime.Domain.Entities.Collection;
using Prime.Domain.Enums;

namespace Prime.Application.Features.Collection;

/// <summary>
/// Void, reversal and correction (docs/analysis/collection.md §4.4–§4.5;
/// CLAUDE.md §46): a request with a reason, decided by someone other than
/// the requester. On approval the payment becomes Voided (approved on the
/// payment's own date) or Reversed (later), gets a cancellation transaction
/// number, and its allocations stop counting, so what it settled is owed
/// again. A correction also posts its replacement in the same transaction,
/// dated like the payment it corrects, so the charges are those of the
/// original payment date (DOMAIN VERIFICATION REQUIRED). Nothing is deleted.
/// </summary>
public sealed partial class PaymentService
{
    private static readonly JsonSerializerOptions ReplacementJson = new(JsonSerializerDefaults.Web);

    public async Task<Result<PaymentCancellationDto>> RequestCancellationAsync(Guid paymentId, RequestPaymentCancellationRequest request,
        CancellationToken cancellationToken = default)
    {
        var payment = await RequestablePaymentAsync(paymentId, request.Reason, cancellationToken);
        if (payment.IsFailure)
        {
            return Result.Failure<PaymentCancellationDto>(payment.Code!, payment.Message!);
        }
        return await SaveRequestAsync(new PaymentCancellation { PaymentId = paymentId, Reason = request.Reason.Trim() }, cancellationToken);
    }

    public async Task<Result<PaymentCancellationDto>> RequestCorrectionAsync(Guid paymentId, RequestPaymentCorrectionRequest request,
        CancellationToken cancellationToken = default)
    {
        var payment = await RequestablePaymentAsync(paymentId, request.Reason, cancellationToken);
        if (payment.IsFailure)
        {
            return Result.Failure<PaymentCancellationDto>(payment.Code!, payment.Message!);
        }

        // Check the replacement now, as if the original were already undone, so a
        // request that could never be approved is refused up front.
        var replacement = ToPostRequest(request.Replacement, $"check:{Guid.NewGuid()}");
        var validation = await postValidator.ValidateAsync(replacement, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure<PaymentCancellationDto>("VALIDATION_FAILED", string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)));
        }
        var allocated = await AllocateAsync(replacement.Items, payment.Value.PaymentDate, cancellationToken, excludePaymentId: paymentId);
        if (allocated.IsFailure)
        {
            return Result.Failure<PaymentCancellationDto>(allocated.Code!, allocated.Message!);
        }
        var amountDue = allocated.Value.Sum(l => l.Allocation.Amount);
        if (amountDue != replacement.ExpectedTotal)
        {
            return Result.Failure<PaymentCancellationDto>("PAYMENT_QUOTE_CHANGED",
                $"The corrected payment comes to {amountDue:N2} as of {payment.Value.PaymentDate:yyyy-MM-dd}, not the {replacement.ExpectedTotal:N2} entered.");
        }
        var tenders = await TendersAsync(replacement.Tenders, amountDue, cancellationToken);
        if (tenders.IsFailure)
        {
            return Result.Failure<PaymentCancellationDto>(tenders.Code!, tenders.Message!);
        }

        return await SaveRequestAsync(new PaymentCancellation
        {
            PaymentId = paymentId,
            Reason = request.Reason.Trim(),
            IsCorrection = true,
            ReplacementRequestJson = JsonSerializer.Serialize(request.Replacement, ReplacementJson),
        }, cancellationToken);
    }

    public async Task<Result<PaymentCancellationDto>> ApproveCancellationAsync(Guid cancellationId, DecidePaymentCancellationRequest request,
        CancellationToken cancellationToken = default)
    {
        var pending = await PendingAsync(cancellationId, request, cancellationToken);
        if (pending.IsFailure)
        {
            return Result.Failure<PaymentCancellationDto>(pending.Code!, pending.Message!);
        }
        var cancellation = pending.Value;
        var replacement = cancellation.IsCorrection
            ? JsonSerializer.Deserialize<PaymentReplacementRequest>(cancellation.ReplacementRequestJson!, ReplacementJson)!
            : null;

        var ownsTransaction = db.Database.CurrentTransaction is null;
        var transaction = ownsTransaction ? await db.Database.BeginTransactionAsync(cancellationToken) : null;
        // Inside a caller's transaction, a failed correction must still leave nothing changed.
        const string savepoint = "payment_cancellation";
        if (!ownsTransaction)
        {
            await db.Database.CurrentTransaction!.CreateSavepointAsync(savepoint, cancellationToken);
        }
        try
        {
            var payment = await db.Payments.Include(x => x.Allocations).SingleAsync(x => x.Id == cancellation.PaymentId, cancellationToken);
            await collectionLock.LockAsync(payment.Allocations.Select(a => (a.RpuId, a.TaxYear))
                .Concat(replacement?.Items.Select(i => (i.RpuId, i.TaxYear)) ?? []), cancellationToken);
            await db.Entry(payment).ReloadAsync(cancellationToken);
            if (payment.Status != PaymentStatus.Posted)
            {
                return Result.Failure<PaymentCancellationDto>("PAYMENT_NOT_POSTED", "The payment is no longer posted; it cannot be cancelled again.");
            }

            var now = clock.UtcNow;
            var kind = clock.LocalDate(now) == payment.PaymentDate ? PaymentCancellationKind.Void : PaymentCancellationKind.Reversal;
            var context = await NumberContexts.ForPropertyAsync(db, payment.Allocations[0].PropertyId, clock.Today.Year, cancellationToken);
            var number = await numbering.GenerateIfConfiguredAsync(NumberedDocumentKind.PaymentTransaction, context, clock.Today, cancellationToken);
            if (number.IsFailure)
            {
                return Result.Failure<PaymentCancellationDto>(number.Code!, number.Message!);
            }
            if (number.Value is null)
            {
                return Result.Failure<PaymentCancellationDto>("PAYMENT_TRANSACTION_NUMBERING_NOT_CONFIGURED",
                    "No approved PaymentTransaction numbering scheme is in force; a cancellation needs its own transaction number (eOR §7.1).");
            }

            payment.Status = kind == PaymentCancellationKind.Void ? PaymentStatus.Voided : PaymentStatus.Reversed;
            payment.CancelledAt = now;
            cancellation.Status = PaymentCancellationStatus.Approved;
            cancellation.Kind = kind;
            cancellation.DecidedBy = currentUser.AppUserId;
            cancellation.DecidedAt = now;
            cancellation.DecisionRemarks = Trimmed(request.Remarks);
            cancellation.TransactionNumber = number.Value;
            currentUser.Reason = cancellation.Reason;
            await db.SaveChangesAsync(cancellationToken);

            if (replacement is not null)
            {
                var posted = await PostCoreAsync(ToPostRequest(replacement, $"correction:{cancellation.Id}"), payment.PaymentDate, payment.Id, cancellationToken);
                if (posted.IsFailure)
                {
                    if (!ownsTransaction)
                    {
                        await db.Database.CurrentTransaction!.RollbackToSavepointAsync(savepoint, cancellationToken);
                        await db.Entry(payment).ReloadAsync(cancellationToken);
                        await db.Entry(cancellation).ReloadAsync(cancellationToken);
                    }
                    return Result.Failure<PaymentCancellationDto>(posted.Code!,
                        $"The correction was not approved because its replacement could not be posted: {posted.Message}");
                }
                cancellation.ReplacementPaymentId = posted.Value.Id;
                await db.SaveChangesAsync(cancellationToken);
            }

            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }
        }
        catch (DbUpdateException)
        {
            return Result.Failure<PaymentCancellationDto>("PAYMENT_CANCELLATION_CONFLICT",
                "The cancellation could not be saved because another change happened at the same time. Nothing was changed; reload and try again.");
        }
        finally
        {
            if (transaction is not null)
            {
                await transaction.DisposeAsync();
            }
        }
        return Result.Success(await MapCancellationAsync(cancellation.Id, cancellationToken));
    }

    public async Task<Result<PaymentCancellationDto>> RejectCancellationAsync(Guid cancellationId, DecidePaymentCancellationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Remarks))
        {
            return Result.Failure<PaymentCancellationDto>("VALIDATION_FAILED", "Give the reason for rejecting the request.");
        }
        var pending = await PendingAsync(cancellationId, request, cancellationToken);
        if (pending.IsFailure)
        {
            return Result.Failure<PaymentCancellationDto>(pending.Code!, pending.Message!);
        }
        var cancellation = pending.Value;
        cancellation.Status = PaymentCancellationStatus.Rejected;
        cancellation.DecidedBy = currentUser.AppUserId;
        cancellation.DecidedAt = clock.UtcNow;
        cancellation.DecisionRemarks = Trimmed(request.Remarks);
        currentUser.Reason = cancellation.DecisionRemarks;
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success(await MapCancellationAsync(cancellation.Id, cancellationToken));
    }

    public async Task<Result<IReadOnlyList<PaymentCancellationDto>>> ListCancellationsAsync(PaymentCancellationStatus? status,
        CancellationToken cancellationToken = default)
    {
        var query = db.PaymentCancellations.AsNoTracking().Include(x => x.Payment).AsQueryable();
        if (status is { } s)
        {
            query = query.Where(x => x.Status == s);
        }
        var rows = await query.OrderByDescending(x => x.CreatedAt).Take(500).ToListAsync(cancellationToken);
        return Result.Success<IReadOnlyList<PaymentCancellationDto>>(rows.Select(ToCancellationDto).ToList());
    }

    // --- Helpers ---

    private async Task<Result<Payment>> RequestablePaymentAsync(Guid paymentId, string? reason, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > 1000)
        {
            return Result.Failure<Payment>("VALIDATION_FAILED", "A reason is required (max 1000 characters).");
        }
        var payment = await db.Payments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == paymentId, ct);
        if (payment is null)
        {
            return Result.Failure<Payment>("PAYMENT_NOT_FOUND", "No payment was found with the given id.");
        }
        if (payment.Status != PaymentStatus.Posted)
        {
            return Result.Failure<Payment>("PAYMENT_NOT_POSTED", "Only a posted payment can be voided, reversed or corrected.");
        }
        if (await db.PaymentCancellations.AnyAsync(x => x.PaymentId == paymentId && x.Status == PaymentCancellationStatus.Pending, ct))
        {
            return Result.Failure<Payment>("PAYMENT_CANCELLATION_DUPLICATE", "A request for this payment is already waiting for a decision.");
        }
        return Result.Success(payment);
    }

    private async Task<Result<PaymentCancellationDto>> SaveRequestAsync(PaymentCancellation cancellation, CancellationToken ct)
    {
        db.PaymentCancellations.Add(cancellation);
        currentUser.Reason = cancellation.Reason;
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // UX_PaymentCancellations_Pending: a second request raced this one.
            return Result.Failure<PaymentCancellationDto>("PAYMENT_CANCELLATION_DUPLICATE", "A request for this payment is already waiting for a decision.");
        }
        return Result.Success(await MapCancellationAsync(cancellation.Id, ct));
    }

    /// <summary>A pending request the current user may decide (CLAUDE.md §46: never the requester).</summary>
    private async Task<Result<PaymentCancellation>> PendingAsync(Guid cancellationId, DecidePaymentCancellationRequest request, CancellationToken ct)
    {
        if (request.Remarks is { Length: > 1000 })
        {
            return Result.Failure<PaymentCancellation>("VALIDATION_FAILED", "Remarks may not exceed 1000 characters.");
        }
        var cancellation = await db.PaymentCancellations.FirstOrDefaultAsync(x => x.Id == cancellationId, ct);
        if (cancellation is null)
        {
            return Result.Failure<PaymentCancellation>("PAYMENT_CANCELLATION_NOT_FOUND", "No cancellation request was found with the given id.");
        }
        if (cancellation.Status != PaymentCancellationStatus.Pending)
        {
            return Result.Failure<PaymentCancellation>("PAYMENT_CANCELLATION_NOT_PENDING", "The request has already been decided.");
        }
        if (currentUser.AppUserId is not null && cancellation.CreatedBy == currentUser.AppUserId)
        {
            return Result.Failure<PaymentCancellation>("CANNOT_APPROVE_OWN_PAYMENT_CANCELLATION",
                "The user who requested it cannot also decide it (maker-checker, CLAUDE.md §46).");
        }
        return Result.Success(cancellation);
    }

    private static PostPaymentRequest ToPostRequest(PaymentReplacementRequest r, string idempotencyKey) =>
        new(idempotencyKey, r.PayorTaxpayerId, r.PayorName, r.PayorAddress, r.Items, r.Tenders, r.ExpectedTotal, r.OfficialReceiptNumber, r.Remarks);

    private static string? Trimmed(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private async Task<PaymentCancellationDto> MapCancellationAsync(Guid id, CancellationToken ct) =>
        ToCancellationDto(await db.PaymentCancellations.AsNoTracking().Include(x => x.Payment).SingleAsync(x => x.Id == id, ct));

    private static PaymentCancellationDto ToCancellationDto(PaymentCancellation c) => new(
        c.Id, c.PaymentId, c.Payment!.OfficialReceiptNumber, c.Payment.TransactionNumber, c.Payment.PaymentDate, c.Payment.AmountDue,
        c.Payment.PayorName, c.Reason, c.IsCorrection,
        c.ReplacementRequestJson is null ? null : JsonSerializer.Deserialize<PaymentReplacementRequest>(c.ReplacementRequestJson, ReplacementJson),
        c.Status, c.Kind, c.CreatedBy, c.CreatedAt, c.DecidedBy, c.DecidedAt, c.DecisionRemarks, c.TransactionNumber, c.ReplacementPaymentId);
}
