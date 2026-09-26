using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Prime.Application.Common;
using Prime.Application.Common.Interfaces;
using Prime.Application.Features.Numbering;
using Prime.Domain.DomainServices;
using Prime.Domain.Entities.Billing;
using Prime.Domain.Entities.Collection;
using Prime.Domain.Enums;

namespace Prime.Application.Features.Collection;

public interface IPaymentService
{
    Task<Result<OutstandingDto>> GetOutstandingAsync(Guid propertyId, DateOnly? asOf, CancellationToken cancellationToken = default);
    Task<Result<PaymentQuoteDto>> QuoteAsync(QuotePaymentRequest request, CancellationToken cancellationToken = default);
    Task<Result<PaymentDto>> PostAsync(PostPaymentRequest request, CancellationToken cancellationToken = default);
    Task<Result<PaymentDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<PaymentSummaryDto>>> ListByPropertyAsync(Guid propertyId, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<PaymentSummaryDto>>> ListAsync(DateOnly? date, Guid? cashierUserId, PaymentStatus? status, CancellationToken cancellationToken = default);
}

/// <summary>
/// docs/analysis/collection.md §2–§4: resolves what is owed — posted bills,
/// what payments that still stand have settled, the rules in force on each
/// bill's rules date — lets <see cref="CollectionCalculator"/> allocate, codes
/// each line to its revenue account and posts atomically. The service does no
/// arithmetic itself (CLAUDE.md Rule 9).
///
/// Posting holds <see cref="ICollectionLock"/> for every (unit, tax year)
/// touched, so concurrent payments of the same installment are serialized
/// and the second sees the first's allocations.
/// </summary>
public sealed class PaymentService(
    IApplicationDbContext db,
    IValidator<QuotePaymentRequest> quoteValidator,
    IValidator<PostPaymentRequest> postValidator,
    ICurrentUserService currentUser,
    INumberingService numbering,
    ICollectionLock collectionLock,
    IOptions<LguOptions> lgu,
    IClock clock) : IPaymentService
{
    public async Task<Result<OutstandingDto>> GetOutstandingAsync(Guid propertyId, DateOnly? asOf, CancellationToken cancellationToken = default)
    {
        if (!await db.Properties.AnyAsync(x => x.Id == propertyId, cancellationToken))
        {
            return Result.Failure<OutstandingDto>("PROPERTY_NOT_FOUND", "No property was found with the given id.");
        }
        var date = asOf ?? clock.Today;

        var pairs = await db.TaxBills.Where(x => x.PropertyId == propertyId && x.Status == WorkflowStatus.Posted)
            .Select(x => new { x.RpuId, x.TaxYear }).ToListAsync(cancellationToken);
        var bills = await LoadAsync(pairs.Select(p => (p.RpuId, p.TaxYear)), cancellationToken);

        var dtos = new List<OutstandingBillDto>();
        foreach (var (bill, collection) in bills.OrderBy(b => b.Bill.TaxYear).ThenBy(b => b.Bill.Rpu!.RpuNumber))
        {
            var installments = collection.Keys.GroupBy(k => k.InstallmentSequence).OrderBy(g => g.Key).Select(g =>
            {
                var outstanding = g.Sum(k => k.Outstanding);
                decimal? due = outstanding == 0
                    ? null
                    : CollectionCalculator.Allocate(new CollectionInput(date, [collection], [new CollectionItem(bill.RpuId, bill.TaxYear, g.Key, null)])).Total;
                return new OutstandingInstallmentDto(g.Key, g.First().DueDate, g.Sum(k => k.PrincipalOwed), g.Sum(k => k.PrincipalPaid),
                    outstanding, due, g.Any(k => k.PrincipalPaid > k.PrincipalOwed));
            }).ToList();
            dtos.Add(new OutstandingBillDto(bill.Id, bill.BillNumber, bill.RpuId, bill.Rpu!.RpuNumber,
                bill.TaxDeclaration!.TaxDeclarationNumber, bill.TaxYear, installments));
        }

        var collectionBills = bills.Select(b => b.Collection).ToList();
        var everything = CollectionCalculator.AllOutstanding(collectionBills);
        var totalDue = everything.Count == 0 ? 0m : CollectionCalculator.Allocate(new CollectionInput(date, collectionBills, everything)).Total;
        return Result.Success(new OutstandingDto(propertyId, date, dtos,
            dtos.Sum(b => b.Installments.Sum(i => i.Outstanding)), totalDue));
    }

    public async Task<Result<PaymentQuoteDto>> QuoteAsync(QuotePaymentRequest request, CancellationToken cancellationToken = default)
    {
        var validation = await quoteValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure<PaymentQuoteDto>("VALIDATION_FAILED", string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)));
        }
        var today = clock.Today;
        var allocated = await AllocateAsync(request.Items, today, cancellationToken);
        if (allocated.IsFailure)
        {
            return Result.Failure<PaymentQuoteDto>(allocated.Code!, allocated.Message!);
        }
        var lines = allocated.Value;
        return Result.Success(new PaymentQuoteDto(today, lines.Select((l, i) => ToDto(l, i + 1)).ToList(), lines.Sum(l => l.Allocation.Amount)));
    }

    public async Task<Result<PaymentDto>> PostAsync(PostPaymentRequest request, CancellationToken cancellationToken = default)
    {
        var validation = await postValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure<PaymentDto>("VALIDATION_FAILED", string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)));
        }
        if (request.PayorTaxpayerId is { } payorId && !await db.Taxpayers.AnyAsync(x => x.Id == payorId, cancellationToken))
        {
            return Result.Failure<PaymentDto>("TAXPAYER_NOT_FOUND", "No taxpayer was found with the given payor id.");
        }

        var ownsTransaction = db.Database.CurrentTransaction is null;
        var transaction = ownsTransaction ? await db.Database.BeginTransactionAsync(cancellationToken) : null;
        try
        {
            await collectionLock.LockAsync(request.Items.Select(i => (i.RpuId, i.TaxYear)), cancellationToken);

            // Checked under the lock: a repeat submission waits for the first, then finds it.
            if (await db.Payments.AsNoTracking().FirstOrDefaultAsync(x => x.IdempotencyKey == request.IdempotencyKey, cancellationToken) is { } existing)
            {
                return existing.AmountDue == request.ExpectedTotal
                    ? Result.Success(await MapAsync(existing.Id, cancellationToken))
                    : Result.Failure<PaymentDto>("PAYMENT_IDEMPOTENCY_CONFLICT",
                        "This submission key was already used for a different payment. Start a new payment.");
            }

            var paymentDate = clock.Today;
            var allocated = await AllocateAsync(request.Items, paymentDate, cancellationToken);
            if (allocated.IsFailure)
            {
                return Result.Failure<PaymentDto>(allocated.Code!, allocated.Message!);
            }
            var lines = allocated.Value;
            var amountDue = lines.Sum(l => l.Allocation.Amount);
            if (amountDue != request.ExpectedTotal)
            {
                return Result.Failure<PaymentDto>("PAYMENT_QUOTE_CHANGED",
                    $"The amount due is now {amountDue:N2}, not the {request.ExpectedTotal:N2} confirmed. Review the new quote and confirm again.");
            }

            var tenders = await TendersAsync(request.Tenders, amountDue, cancellationToken);
            if (tenders.IsFailure)
            {
                return Result.Failure<PaymentDto>(tenders.Code!, tenders.Message!);
            }
            var tendered = request.Tenders.Sum(t => t.Amount);

            var context = await NumberContexts.ForPropertyAsync(db, lines[0].Bill.PropertyId, paymentDate.Year, cancellationToken);
            var transactionNumber = await numbering.GenerateIfConfiguredAsync(NumberedDocumentKind.PaymentTransaction, context, paymentDate, cancellationToken);
            if (transactionNumber.IsFailure)
            {
                return Result.Failure<PaymentDto>(transactionNumber.Code!, transactionNumber.Message!);
            }
            if (transactionNumber.Value is null)
            {
                return Result.Failure<PaymentDto>("PAYMENT_TRANSACTION_NUMBERING_NOT_CONFIGURED",
                    "No approved PaymentTransaction numbering scheme is in force; every collection needs a system-generated transaction number (eOR §7.1).");
            }
            var receiptNumber = await numbering.AssignAsync(NumberedDocumentKind.OfficialReceipt, context, request.OfficialReceiptNumber, paymentDate, cancellationToken);
            if (receiptNumber.IsFailure)
            {
                return Result.Failure<PaymentDto>(receiptNumber.Code!, receiptNumber.Message!);
            }
            if (await db.Payments.AnyAsync(x => x.OfficialReceiptNumber == receiptNumber.Value, cancellationToken))
            {
                return Result.Failure<PaymentDto>("PAYMENT_OR_NUMBER_DUPLICATE", $"Official receipt {receiptNumber.Value} has already been issued.");
            }

            var payment = new Payment
            {
                TransactionNumber = transactionNumber.Value,
                OfficialReceiptNumber = receiptNumber.Value,
                IdempotencyKey = request.IdempotencyKey,
                PayorTaxpayerId = request.PayorTaxpayerId,
                PayorName = request.PayorName.Trim(),
                PayorAddress = string.IsNullOrWhiteSpace(request.PayorAddress) ? null : request.PayorAddress.Trim(),
                PaymentDate = paymentDate,
                ReceivedAt = clock.UtcNow,
                Office = lgu.Value.Office,
                LocationCode = lgu.Value.LocationCode,
                CashierUserId = currentUser.AppUserId,
                AmountDue = amountDue,
                AmountTendered = tendered,
                Change = tendered - amountDue,
                Remarks = string.IsNullOrWhiteSpace(request.Remarks) ? null : request.Remarks.Trim(),
                Tenders = tenders.Value,
                Allocations = lines.Select((l, i) => new PaymentAllocation
                {
                    LineNumber = i + 1,
                    TaxBillId = l.Bill.Id,
                    PropertyId = l.Bill.PropertyId,
                    RpuId = l.Allocation.RpuId,
                    TaxYear = l.Allocation.TaxYear,
                    InstallmentSequence = l.Allocation.InstallmentSequence,
                    DueDate = l.Allocation.DueDate,
                    TaxTypeId = l.Allocation.TaxTypeId,
                    Component = l.Allocation.Component,
                    RuleId = l.Allocation.RuleId,
                    RatePercent = l.Allocation.RatePercent,
                    BaseAmount = l.Allocation.BaseAmount,
                    Amount = l.Allocation.Amount,
                    Months = l.Allocation.Months,
                    YearCategory = l.Allocation.YearCategory,
                    Explanation = l.Allocation.Explanation,
                    RevenueAccountMappingId = l.Account.Id,
                    AccountCode = l.Account.AccountCode,
                    AccountName = l.Account.AccountName,
                    Fund = l.Account.Fund,
                }).ToList(),
            };
            db.Payments.Add(payment);
            await db.SaveChangesAsync(cancellationToken);
            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }
            return Result.Success(await MapAsync(payment.Id, cancellationToken));
        }
        catch (DbUpdateException)
        {
            return Result.Failure<PaymentDto>("PAYMENT_POST_CONFLICT",
                "The payment could not be saved because another change happened at the same time (e.g. the same receipt number). Nothing was posted; reload and try again.");
        }
        finally
        {
            if (transaction is not null)
            {
                await transaction.DisposeAsync();
            }
        }
    }

    public async Task<Result<PaymentDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await db.Payments.AnyAsync(x => x.Id == id, cancellationToken)
            ? Result.Success(await MapAsync(id, cancellationToken))
            : Result.Failure<PaymentDto>("PAYMENT_NOT_FOUND", "No payment was found with the given id.");

    public async Task<Result<IReadOnlyList<PaymentSummaryDto>>> ListByPropertyAsync(Guid propertyId, CancellationToken cancellationToken = default) =>
        Result.Success<IReadOnlyList<PaymentSummaryDto>>(await Summaries(db.Payments
                .Where(p => p.Allocations.Any(a => a.PropertyId == propertyId)))
            .ToListAsync(cancellationToken));

    public async Task<Result<IReadOnlyList<PaymentSummaryDto>>> ListAsync(DateOnly? date, Guid? cashierUserId, PaymentStatus? status,
        CancellationToken cancellationToken = default)
    {
        var day = date ?? clock.Today;
        var query = db.Payments.Where(p => p.PaymentDate == day);
        if (cashierUserId is { } cashier)
        {
            query = query.Where(p => p.CashierUserId == cashier);
        }
        if (status is { } s)
        {
            query = query.Where(p => p.Status == s);
        }
        return Result.Success<IReadOnlyList<PaymentSummaryDto>>(await Summaries(query).ToListAsync(cancellationToken));
    }

    // --- Resolution ---

    private sealed record LoadedBill(TaxBill Bill, CollectionBill Collection);

    private sealed record AllocatedLine(TaxBill Bill, CollectionAllocation Allocation, RevenueAccountMapping Account);

    /// <summary>
    /// The posted bill for each (unit, tax year), what standing payments have
    /// settled on it, and the rules in force on its rules date. Pairs without a
    /// posted bill are left out (the calculator reports them).
    /// </summary>
    private async Task<List<LoadedBill>> LoadAsync(IEnumerable<(Guid RpuId, int TaxYear)> pairs, CancellationToken ct)
    {
        var wanted = pairs.Distinct().ToList();
        var rpuIds = wanted.Select(p => p.RpuId).Distinct().ToList();
        var years = wanted.Select(p => p.TaxYear).Distinct().ToList();

        var bills = (await db.TaxBills.AsNoTracking()
                .Include(x => x.Rpu).Include(x => x.TaxDeclaration)
                .Include(x => x.Details).ThenInclude(d => d.TaxType)
                .Where(x => x.Status == WorkflowStatus.Posted && rpuIds.Contains(x.RpuId) && years.Contains(x.TaxYear))
                .ToListAsync(ct))
            .Where(b => wanted.Contains((b.RpuId, b.TaxYear)))
            .ToList();

        var settled = await db.PaymentAllocations.AsNoTracking()
            .Where(a => rpuIds.Contains(a.RpuId) && years.Contains(a.TaxYear)
                && (a.Component == BillingComponent.Tax || (a.Component == BillingComponent.Penalty && a.RatePercent == null))
                && db.Payments.Any(p => p.Id == a.PaymentId && p.Status == PaymentStatus.Posted))
            .Select(a => new { a.RpuId, a.TaxYear, a.InstallmentSequence, a.TaxTypeId, a.Component, a.Amount })
            .ToListAsync(ct);

        var loaded = new List<LoadedBill>();
        foreach (var bill in bills.OrderBy(b => b.TaxYear).ThenBy(b => b.Rpu!.RpuNumber))
        {
            var date = bill.RulesAsOfDate;
            var keys = bill.Details.Where(d => d.Component == BillingComponent.Tax).OrderBy(d => d.LineNumber).Select(d =>
            {
                var paid = settled.Where(a => a.RpuId == bill.RpuId && a.TaxYear == bill.TaxYear
                    && a.InstallmentSequence == d.InstallmentSequence && a.TaxTypeId == d.TaxTypeId).ToList();
                return new CollectionKey(d.InstallmentSequence, d.TaxTypeId, d.RuleId, d.DueDate, d.Amount,
                    paid.Where(a => a.Component == BillingComponent.Tax).Sum(a => a.Amount),
                    paid.Any(a => a.Component == BillingComponent.Penalty));
            }).ToList();

            loaded.Add(new LoadedBill(bill, new CollectionBill(bill.Id, bill.RpuId, bill.TaxYear, bill.DiscountStackingAllowed,
                await InForce(db.DiscountRules, date).ToListAsync(ct),
                await InForce(db.PenaltyRules, date).ToListAsync(ct),
                await InForce(db.InterestRules, date).ToListAsync(ct),
                keys)));
        }
        return loaded;
    }

    /// <summary>Allocates <paramref name="items"/> on <paramref name="paymentDate"/> and codes every line to its revenue account (decision 5: refuse when unmapped).</summary>
    private async Task<Result<List<AllocatedLine>>> AllocateAsync(IReadOnlyList<PaymentItemRequest> items, DateOnly paymentDate, CancellationToken ct)
    {
        var loaded = await LoadAsync(items.Select(i => (i.RpuId, i.TaxYear)), ct);
        var input = new CollectionInput(paymentDate, loaded.Select(l => l.Collection).ToList(),
            items.Select(i => new CollectionItem(i.RpuId, i.TaxYear, i.InstallmentSequence, i.PrincipalAmount)).ToList());
        if (CollectionCalculator.Validate(input) is { } problem)
        {
            var code = problem.Kind switch
            {
                CollectionProblemKind.UnknownInstallment => "PAYMENT_BILL_NOT_FOUND",
                CollectionProblemKind.AlreadySettled => "PAYMENT_ALREADY_SETTLED",
                _ => "PAYMENT_INVALID_SELECTION",
            };
            return Result.Failure<List<AllocatedLine>>(code, char.ToUpperInvariant(problem.Message[0]) + problem.Message[1..] + ".");
        }

        var result = CollectionCalculator.Allocate(input);
        var mappings = await db.RevenueAccountMappings.AsNoTracking().Include(x => x.TaxType)
            .InForce(paymentDate).ToListAsync(ct);
        var taxTypes = await db.TaxTypes.AsNoTracking().ToDictionaryAsync(x => x.Id, x => x.Code, ct);

        var lines = new List<AllocatedLine>();
        var missing = new SortedSet<string>();
        foreach (var allocation in result.Allocations)
        {
            var account = mappings.SingleOrDefault(m => m.TaxTypeId == allocation.TaxTypeId
                && m.Component == allocation.Component && m.YearCategory == allocation.YearCategory);
            if (account is null)
            {
                missing.Add($"{taxTypes.GetValueOrDefault(allocation.TaxTypeId, allocation.TaxTypeId.ToString())} {allocation.Component} ({allocation.YearCategory})");
                continue;
            }
            lines.Add(new AllocatedLine(loaded.Single(l => l.Bill.Id == allocation.BillId).Bill, allocation, account));
        }
        if (missing.Count > 0)
        {
            return Result.Failure<List<AllocatedLine>>("PAYMENT_ACCOUNT_NOT_MAPPED",
                $"No approved revenue account is in force on {paymentDate:yyyy-MM-dd} for: {string.Join("; ", missing)}. Every receipt line must be coded to a revenue account (eOR §7.1).");
        }
        return Result.Success(lines);
    }

    /// <summary>Tenders must cover the amount due; change can come only from modes that allow it.</summary>
    private async Task<Result<List<PaymentTender>>> TendersAsync(IReadOnlyList<PaymentTenderRequest> requested, decimal amountDue, CancellationToken ct)
    {
        var modeIds = requested.Select(t => t.PaymentModeId).Distinct().ToList();
        var modes = await db.PaymentModes.AsNoTracking().Where(x => modeIds.Contains(x.Id) && x.IsActive).ToDictionaryAsync(x => x.Id, ct);
        if (modes.Count != modeIds.Count)
        {
            return Result.Failure<List<PaymentTender>>("PAYMENT_MODE_NOT_FOUND", "A mode of payment is unknown or no longer accepted.");
        }
        if (requested.FirstOrDefault(t => modes[t.PaymentModeId].RequiresReference && string.IsNullOrWhiteSpace(t.Reference)) is { } unreferenced)
        {
            return Result.Failure<List<PaymentTender>>("PAYMENT_TENDER_INVALID", $"{modes[unreferenced.PaymentModeId].Name} needs a reference (e.g. the check number).");
        }

        var tendered = requested.Sum(t => t.Amount);
        if (tendered < amountDue)
        {
            return Result.Failure<List<PaymentTender>>("PAYMENT_TENDER_INVALID", $"The amount tendered ({tendered:N2}) is less than the amount due ({amountDue:N2}).");
        }
        var changeable = requested.Where(t => modes[t.PaymentModeId].AllowsChange).Sum(t => t.Amount);
        if (tendered - amountDue > changeable)
        {
            return Result.Failure<List<PaymentTender>>("PAYMENT_TENDER_INVALID",
                $"Change of {tendered - amountDue:N2} cannot be given from these modes of payment; non-cash tenders must not exceed the amount due.");
        }

        return Result.Success(requested.Select(t => new PaymentTender
        {
            PaymentModeId = t.PaymentModeId,
            Amount = t.Amount,
            Reference = string.IsNullOrWhiteSpace(t.Reference) ? null : t.Reference.Trim(),
            Bank = string.IsNullOrWhiteSpace(t.Bank) ? null : t.Bank.Trim(),
            CheckDate = t.CheckDate,
        }).ToList());
    }

    private static IQueryable<T> InForce<T>(IQueryable<T> query, DateOnly date) where T : BillingRule =>
        query.Where(x => x.Status == WorkflowStatus.Approved && x.EffectiveDate <= date && (x.EndDate == null || x.EndDate >= date));

    // --- Mapping ---

    private static IQueryable<PaymentSummaryDto> Summaries(IQueryable<Payment> query) => query
        .OrderByDescending(p => p.ReceivedAt)
        .Select(p => new PaymentSummaryDto(p.Id, p.TransactionNumber, p.OfficialReceiptNumber, p.PayorName, p.PaymentDate, p.ReceivedAt,
            p.CashierUserId, p.AmountDue, p.Status));

    private async Task<PaymentDto> MapAsync(Guid id, CancellationToken ct)
    {
        var p = await db.Payments.AsNoTracking()
            .Include(x => x.Tenders).ThenInclude(t => t.PaymentMode)
            .Include(x => x.Allocations).ThenInclude(a => a.TaxType)
            .Include(x => x.Allocations).ThenInclude(a => a.TaxBill!).ThenInclude(b => b.Rpu)
            .Include(x => x.Allocations).ThenInclude(a => a.TaxBill!).ThenInclude(b => b.TaxDeclaration)
            .SingleAsync(x => x.Id == id, ct);
        return new PaymentDto(p.Id, p.TransactionNumber, p.OfficialReceiptNumber, p.PayorTaxpayerId, p.PayorName, p.PayorAddress,
            p.PaymentDate, p.ReceivedAt, p.Office, p.LocationCode, p.CashierUserId, p.AmountDue, p.AmountTendered, p.Change, p.Status, p.Remarks,
            p.Tenders.Select(t => new PaymentTenderDto(t.PaymentModeId, t.PaymentMode!.Code, t.PaymentMode.Name, t.Amount, t.Reference, t.Bank, t.CheckDate)).ToList(),
            p.Allocations.OrderBy(a => a.LineNumber).Select(a => new PaymentAllocationDto(a.LineNumber, a.TaxBillId, a.TaxBill!.BillNumber,
                a.TaxBill.TaxDeclaration!.TaxDeclarationNumber, a.PropertyId, a.RpuId, a.TaxBill.Rpu!.RpuNumber, a.TaxYear, a.InstallmentSequence,
                a.DueDate, a.TaxTypeId, a.TaxType!.Code, a.TaxType.Name, a.Component, a.RuleId, a.RatePercent, a.BaseAmount, a.Amount, a.Months,
                a.YearCategory, a.Explanation, a.AccountCode, a.AccountName, a.Fund)).ToList());
    }

    private static PaymentAllocationDto ToDto(AllocatedLine l, int lineNumber)
    {
        var a = l.Allocation;
        var taxType = l.Bill.Details.First(d => d.TaxTypeId == a.TaxTypeId).TaxType!;
        return new PaymentAllocationDto(lineNumber, l.Bill.Id, l.Bill.BillNumber, l.Bill.TaxDeclaration!.TaxDeclarationNumber, l.Bill.PropertyId,
            a.RpuId, l.Bill.Rpu!.RpuNumber, a.TaxYear, a.InstallmentSequence, a.DueDate, a.TaxTypeId, taxType.Code, taxType.Name, a.Component,
            a.RuleId, a.RatePercent, a.BaseAmount, a.Amount, a.Months, a.YearCategory, a.Explanation,
            l.Account.AccountCode, l.Account.AccountName, l.Account.Fund);
    }
}
