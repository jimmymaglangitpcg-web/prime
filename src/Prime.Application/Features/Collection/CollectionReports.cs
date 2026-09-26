using Microsoft.EntityFrameworkCore;
using Prime.Application.Common;
using Prime.Application.Common.Interfaces;
using Prime.Application.Features.Numbering;
using Prime.Domain.DomainServices;
using Prime.Domain.Entities.Collection;
using Prime.Domain.Enums;

namespace Prime.Application.Features.Collection;

// --- DTOs ---

public sealed record CreateRemittanceRequest(DateOnly? CollectionDate, string? Remarks);

public sealed record DecideRemittanceRequest(string? Remarks);

public sealed record RemittanceDto(
    Guid Id,
    string? RemittanceNumber,
    Guid CashierUserId,
    string? CashierName,
    DateOnly CollectionDate,
    RemittanceStatus Status,
    int PaymentCount,
    decimal TotalAmount,
    string? Remarks,
    DateTimeOffset SubmittedAt,
    Guid? DecidedBy,
    DateTimeOffset? DecidedAt,
    string? DecisionRemarks,
    IReadOnlyList<RemittanceModeTotalDto> ModeTotals,
    IReadOnlyList<RemittanceAccountTotalDto> AccountTotals,
    IReadOnlyList<RemittanceItemDto> Items);

public sealed record RemittanceModeTotalDto(Guid PaymentModeId, string ModeCode, string ModeName, decimal Amount);

public sealed record RemittanceAccountTotalDto(string AccountCode, string AccountName, string? Fund, decimal Amount);

public sealed record RemittanceItemDto(Guid PaymentId, string OfficialReceiptNumber, string PayorName, decimal Amount, PaymentStatus PaymentStatus);

public enum CollectionGroupBy
{
    Date,
    Cashier,
    Mode,
    TaxType,
    TaxYear,
    YearCategory,
    Fund,
    Account,
    Barangay,
}

/// <summary>
/// Collections in a date range grouped one way (CLAUDE.md §41). <see cref="Collected"/>
/// counts standing and later-reversed receipts on their payment date;
/// <see cref="Reversed"/> is negative, on the date the reversal was approved.
/// Voided receipts are never counted.
/// </summary>
public sealed record CollectionSummaryDto(DateOnly From, DateOnly To, CollectionGroupBy GroupBy, IReadOnlyList<CollectionSummaryRowDto> Rows,
    decimal TotalCollected, decimal TotalReversed, decimal Net);

public sealed record CollectionSummaryRowDto(string Key, string Label, int Receipts, decimal Collected, decimal Reversed, decimal Net);

/// <summary>One cashier's day: every figure that must agree, and what does not (§4.7).</summary>
public sealed record ReconciliationRowDto(
    Guid? CashierUserId,
    string? CashierName,
    int Receipts,
    decimal AmountDue,
    decimal AllocationTotal,
    decimal TenderedLessChange,
    decimal Remitted,
    decimal Unremitted,
    int UnremittedReceipts,
    int VoidedReceipts,
    IReadOnlyList<string> Problems);

public sealed record ReconciliationDto(DateOnly Date, IReadOnlyList<ReconciliationRowDto> Cashiers, bool Balanced);

// --- Service ---

public interface ICollectionReportService
{
    Task<Result<RemittanceDto>> CreateRemittanceAsync(CreateRemittanceRequest request, CancellationToken cancellationToken = default);
    Task<Result<RemittanceDto>> AcceptRemittanceAsync(Guid id, DecideRemittanceRequest request, CancellationToken cancellationToken = default);
    Task<Result<RemittanceDto>> ReturnRemittanceAsync(Guid id, DecideRemittanceRequest request, CancellationToken cancellationToken = default);
    Task<Result<RemittanceDto>> GetRemittanceAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<RemittanceDto>>> ListRemittancesAsync(DateOnly? date, RemittanceStatus? status, CancellationToken cancellationToken = default);
    Task<Result<CollectionSummaryDto>> SummaryAsync(DateOnly from, DateOnly to, CollectionGroupBy groupBy, CancellationToken cancellationToken = default);
    Task<Result<ReconciliationDto>> ReconcileAsync(DateOnly? date, CancellationToken cancellationToken = default);
}

/// <summary>
/// Remittance, the collection summary and reconciliation (docs/analysis/collection.md
/// §4.7, step 9e). Remitting takes the acting cashier's posted, unremitted
/// receipts of one date and freezes their totals; each payment's row version
/// (xmin) makes remitting and voiding the same receipt at once impossible.
/// A remittance is accepted or returned by someone other than the cashier.
/// </summary>
public sealed class CollectionReportService(
    IApplicationDbContext db,
    ICurrentUserService currentUser,
    INumberingService numbering,
    IClock clock) : ICollectionReportService
{
    /// <summary>The longest summary range, so a report never loads an unbounded set (CLAUDE.md §71).</summary>
    public const int MaxSummaryDays = 366;

    public async Task<Result<RemittanceDto>> CreateRemittanceAsync(CreateRemittanceRequest request, CancellationToken cancellationToken = default)
    {
        if (currentUser.AppUserId is not { } cashier)
        {
            return Result.Failure<RemittanceDto>("REMITTANCE_NO_USER", "Only a signed-in cashier can remit.");
        }
        if (request.Remarks is { Length: > 1000 })
        {
            return Result.Failure<RemittanceDto>("VALIDATION_FAILED", "Remarks may not exceed 1000 characters.");
        }
        var date = request.CollectionDate ?? clock.Today;

        var payments = await db.Payments
            .Include(p => p.Tenders).ThenInclude(t => t.PaymentMode)
            .Include(p => p.Allocations)
            .Where(p => p.CashierUserId == cashier && p.PaymentDate == date && p.Status == PaymentStatus.Posted && p.RemittanceId == null)
            .OrderBy(p => p.ReceivedAt)
            .ToListAsync(cancellationToken);
        if (payments.Count == 0)
        {
            return Result.Failure<RemittanceDto>("REMITTANCE_NOTHING_TO_REMIT", $"You have no unremitted posted receipts dated {date:yyyy-MM-dd}.");
        }
        var ids = payments.Select(p => p.Id).ToList();
        if (await db.PaymentCancellations.AnyAsync(c => ids.Contains(c.PaymentId) && c.Status == PaymentCancellationStatus.Pending, cancellationToken))
        {
            return Result.Failure<RemittanceDto>("REMITTANCE_PENDING_CANCELLATIONS",
                "Some of these receipts have a void or correction request waiting for a decision. Have it decided first.");
        }

        var modeTotals = payments
            .SelectMany(p => TenderNetting.Net(p.Tenders.Select(t => new TenderNetting.Tender(t.PaymentModeId, t.Amount, t.PaymentMode!.AllowsChange)).ToList(), p.Change))
            .GroupBy(x => x.PaymentModeId)
            .Select(g => new RemittanceModeTotal { PaymentModeId = g.Key, Amount = g.Sum(x => x.Amount) })
            .Where(x => x.Amount != 0)
            .ToList();
        var accountTotals = payments.SelectMany(p => p.Allocations)
            .GroupBy(a => new { a.AccountCode, a.AccountName, a.Fund })
            .OrderBy(g => g.Key.AccountCode)
            .Select(g => new RemittanceAccountTotal { AccountCode = g.Key.AccountCode, AccountName = g.Key.AccountName, Fund = g.Key.Fund, Amount = g.Sum(a => a.Amount) })
            .ToList();

        var ownsTransaction = db.Database.CurrentTransaction is null;
        var transaction = ownsTransaction ? await db.Database.BeginTransactionAsync(cancellationToken) : null;
        try
        {
            var number = await numbering.GenerateIfConfiguredAsync(NumberedDocumentKind.Remittance, new NumberContext(date.Year), date, cancellationToken);
            if (number.IsFailure)
            {
                return Result.Failure<RemittanceDto>(number.Code!, number.Message!);
            }
            var remittance = new Remittance
            {
                RemittanceNumber = number.Value,
                CashierUserId = cashier,
                CollectionDate = date,
                PaymentCount = payments.Count,
                TotalAmount = payments.Sum(p => p.AmountDue),
                Remarks = string.IsNullOrWhiteSpace(request.Remarks) ? null : request.Remarks.Trim(),
                Items = payments.Select(p => new RemittanceItem { PaymentId = p.Id, Amount = p.AmountDue }).ToList(),
                ModeTotals = modeTotals,
                AccountTotals = accountTotals,
            };
            db.Remittances.Add(remittance);
            foreach (var payment in payments)
            {
                payment.RemittanceId = remittance.Id;
            }
            await db.SaveChangesAsync(cancellationToken);
            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }
            return Result.Success(await MapAsync(remittance.Id, cancellationToken));
        }
        catch (DbUpdateException)
        {
            // A receipt changed meanwhile (e.g. a void approved at the same moment): its row version no longer matched.
            return Result.Failure<RemittanceDto>("REMITTANCE_CONFLICT",
                "A receipt changed while it was being remitted (for example it was voided). Nothing was remitted; try again.");
        }
        finally
        {
            if (transaction is not null)
            {
                await transaction.DisposeAsync();
            }
        }
    }

    public Task<Result<RemittanceDto>> AcceptRemittanceAsync(Guid id, DecideRemittanceRequest request, CancellationToken cancellationToken = default) =>
        DecideAsync(id, request, accept: true, cancellationToken);

    public Task<Result<RemittanceDto>> ReturnRemittanceAsync(Guid id, DecideRemittanceRequest request, CancellationToken cancellationToken = default) =>
        DecideAsync(id, request, accept: false, cancellationToken);

    private async Task<Result<RemittanceDto>> DecideAsync(Guid id, DecideRemittanceRequest request, bool accept, CancellationToken ct)
    {
        if (!accept && string.IsNullOrWhiteSpace(request.Remarks))
        {
            return Result.Failure<RemittanceDto>("VALIDATION_FAILED", "Give the reason for returning the remittance.");
        }
        if (request.Remarks is { Length: > 1000 })
        {
            return Result.Failure<RemittanceDto>("VALIDATION_FAILED", "Remarks may not exceed 1000 characters.");
        }
        var remittance = await db.Remittances.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (remittance is null)
        {
            return Result.Failure<RemittanceDto>("REMITTANCE_NOT_FOUND", "No remittance was found with the given id.");
        }
        if (remittance.Status != RemittanceStatus.Submitted)
        {
            return Result.Failure<RemittanceDto>("REMITTANCE_NOT_SUBMITTED", "The remittance has already been accepted or returned.");
        }
        if (currentUser.AppUserId is not null && (remittance.CreatedBy == currentUser.AppUserId || remittance.CashierUserId == currentUser.AppUserId))
        {
            return Result.Failure<RemittanceDto>("CANNOT_DECIDE_OWN_REMITTANCE", "The cashier cannot accept or return their own remittance (CLAUDE.md §46).");
        }

        remittance.Status = accept ? RemittanceStatus.Accepted : RemittanceStatus.Returned;
        remittance.DecidedBy = currentUser.AppUserId;
        remittance.DecidedAt = clock.UtcNow;
        remittance.DecisionRemarks = string.IsNullOrWhiteSpace(request.Remarks) ? null : request.Remarks.Trim();
        currentUser.Reason = remittance.DecisionRemarks;
        if (!accept)
        {
            // Returned: its receipts are free for a later remittance (the items keep the history).
            foreach (var payment in await db.Payments.Where(p => p.RemittanceId == remittance.Id).ToListAsync(ct))
            {
                payment.RemittanceId = null;
            }
        }
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Result.Failure<RemittanceDto>("REMITTANCE_CONFLICT", "The remittance or one of its receipts changed at the same time. Reload and try again.");
        }
        return Result.Success(await MapAsync(remittance.Id, ct));
    }

    public async Task<Result<RemittanceDto>> GetRemittanceAsync(Guid id, CancellationToken cancellationToken = default) =>
        await db.Remittances.AnyAsync(x => x.Id == id, cancellationToken)
            ? Result.Success(await MapAsync(id, cancellationToken))
            : Result.Failure<RemittanceDto>("REMITTANCE_NOT_FOUND", "No remittance was found with the given id.");

    public async Task<Result<IReadOnlyList<RemittanceDto>>> ListRemittancesAsync(DateOnly? date, RemittanceStatus? status, CancellationToken cancellationToken = default)
    {
        var query = WithDetails();
        if (date is { } d)
        {
            query = query.Where(x => x.CollectionDate == d);
        }
        if (status is { } s)
        {
            query = query.Where(x => x.Status == s);
        }
        var rows = await query.OrderByDescending(x => x.CreatedAt).Take(200).ToListAsync(cancellationToken);
        var names = await NamesAsync(rows.Select(r => (Guid?)r.CashierUserId), cancellationToken);
        return Result.Success<IReadOnlyList<RemittanceDto>>(rows.Select(r => ToDto(r, names)).ToList());
    }

    public async Task<Result<CollectionSummaryDto>> SummaryAsync(DateOnly from, DateOnly to, CollectionGroupBy groupBy, CancellationToken cancellationToken = default)
    {
        if (to < from || to.DayNumber - from.DayNumber >= MaxSummaryDays)
        {
            return Result.Failure<CollectionSummaryDto>("VALIDATION_FAILED", $"Choose a range of 1 to {MaxSummaryDays} days, ending on or after its start.");
        }

        // Collected: receipts dated in the range that were not voided (a reversal is shown separately, on its own date).
        var collected = await db.Payments.AsNoTracking()
            .Where(p => p.PaymentDate >= from && p.PaymentDate <= to && p.Status != PaymentStatus.Voided)
            .Select(p => new PaymentRow(p.Id, p.PaymentDate, p.CashierUserId, p.AmountDue, p.Change))
            .ToListAsync(cancellationToken);
        // Reversed: approved in the range (LGU dates), whatever the payment's own date.
        var windowStart = new DateTimeOffset(from.AddDays(-1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var windowEnd = new DateTimeOffset(to.AddDays(2).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var reversed = (await db.Payments.AsNoTracking()
                .Where(p => p.Status == PaymentStatus.Reversed && p.CancelledAt >= windowStart && p.CancelledAt < windowEnd)
                .Select(p => new { Row = new PaymentRow(p.Id, p.PaymentDate, p.CashierUserId, p.AmountDue, p.Change), p.CancelledAt })
                .ToListAsync(cancellationToken))
            .Select(x => (x.Row, Date: clock.LocalDate(x.CancelledAt!.Value)))
            .Where(x => x.Date >= from && x.Date <= to)
            .ToList();

        var entries = new List<(string Key, string Label, Guid PaymentId, decimal Amount, bool Reversal)>();
        var allIds = collected.Select(c => c.Id).Concat(reversed.Select(r => r.Row.Id)).Distinct().ToList();
        switch (groupBy)
        {
            case CollectionGroupBy.Date:
                entries.AddRange(collected.Select(c => (c.PaymentDate.ToString("yyyy-MM-dd"), c.PaymentDate.ToString("yyyy-MM-dd"), c.Id, c.AmountDue, false)));
                entries.AddRange(reversed.Select(r => (r.Date.ToString("yyyy-MM-dd"), r.Date.ToString("yyyy-MM-dd"), r.Row.Id, -r.Row.AmountDue, true)));
                break;
            case CollectionGroupBy.Cashier:
                var names = await NamesAsync(collected.Select(c => c.CashierUserId).Concat(reversed.Select(r => r.Row.CashierUserId)), cancellationToken);
                string CashierLabel(Guid? id) => id is { } g ? names.GetValueOrDefault(g) ?? g.ToString() : "(no user)";
                entries.AddRange(collected.Select(c => (c.CashierUserId?.ToString() ?? "-", CashierLabel(c.CashierUserId), c.Id, c.AmountDue, false)));
                entries.AddRange(reversed.Select(r => (r.Row.CashierUserId?.ToString() ?? "-", CashierLabel(r.Row.CashierUserId), r.Row.Id, -r.Row.AmountDue, true)));
                break;
            case CollectionGroupBy.Mode:
                var tenders = await db.PaymentTenders.AsNoTracking().Where(t => allIds.Contains(t.PaymentId))
                    .Select(t => new { t.PaymentId, t.PaymentModeId, t.Amount, t.PaymentMode!.AllowsChange, t.PaymentMode.Code, t.PaymentMode.Name })
                    .ToListAsync(cancellationToken);
                var modeNames = tenders.GroupBy(t => t.PaymentModeId).ToDictionary(g => g.Key, g => (g.First().Code, g.First().Name));
                IEnumerable<(Guid Mode, decimal Amount)> Net(PaymentRow p) => TenderNetting.Net(
                    tenders.Where(t => t.PaymentId == p.Id).Select(t => new TenderNetting.Tender(t.PaymentModeId, t.Amount, t.AllowsChange)).ToList(), p.Change);
                entries.AddRange(collected.SelectMany(c => Net(c).Select(n => (modeNames[n.Mode].Code, modeNames[n.Mode].Name, c.Id, n.Amount, false))));
                entries.AddRange(reversed.SelectMany(r => Net(r.Row).Select(n => (modeNames[n.Mode].Code, modeNames[n.Mode].Name, r.Row.Id, -n.Amount, true))));
                break;
            default:
                var lines = await db.PaymentAllocations.AsNoTracking().Where(a => allIds.Contains(a.PaymentId))
                    .Select(a => new LineRow(a.PaymentId, a.Amount, a.TaxYear, a.YearCategory, a.AccountCode, a.AccountName, a.Fund,
                        a.TaxType!.Code, a.TaxType.Name,
                        db.Properties.Where(x => x.Id == a.PropertyId).Select(x => x.Barangay!.Name).FirstOrDefault()))
                    .ToListAsync(cancellationToken);
                (string Key, string Label) Dimension(LineRow a) => groupBy switch
                {
                    CollectionGroupBy.TaxType => (a.TaxTypeCode, $"{a.TaxTypeCode} — {a.TaxTypeName}"),
                    CollectionGroupBy.TaxYear => (a.TaxYear.ToString(), a.TaxYear.ToString()),
                    CollectionGroupBy.YearCategory => (a.YearCategory.ToString(), a.YearCategory.ToString()),
                    CollectionGroupBy.Fund => (a.Fund ?? "-", a.Fund ?? "(no fund)"),
                    CollectionGroupBy.Account => (a.AccountCode, $"{a.AccountCode} — {a.AccountName}"),
                    CollectionGroupBy.Barangay => (a.Barangay ?? "-", a.Barangay ?? "(unknown)"),
                    _ => throw new InvalidOperationException($"Unhandled {nameof(CollectionGroupBy)}: {groupBy}"),
                };
                var collectedIds = collected.Select(c => c.Id).ToHashSet();
                foreach (var a in lines.Where(l => collectedIds.Contains(l.PaymentId)))
                {
                    var (key, label) = Dimension(a);
                    entries.Add((key, label, a.PaymentId, a.Amount, false));
                }
                var reversedIds = reversed.Select(r => r.Row.Id).ToHashSet();
                foreach (var a in lines.Where(l => reversedIds.Contains(l.PaymentId)))
                {
                    var (key, label) = Dimension(a);
                    entries.Add((key, label, a.PaymentId, -a.Amount, true));
                }
                break;
        }

        var rows = entries.GroupBy(e => e.Key).OrderBy(g => g.Key, StringComparer.Ordinal).Select(g =>
        {
            var collectedAmount = g.Where(e => !e.Reversal).Sum(e => e.Amount);
            var reversedAmount = g.Where(e => e.Reversal).Sum(e => e.Amount);
            return new CollectionSummaryRowDto(g.Key, g.First().Label, g.Where(e => !e.Reversal).Select(e => e.PaymentId).Distinct().Count(),
                collectedAmount, reversedAmount, collectedAmount + reversedAmount);
        }).ToList();
        return Result.Success(new CollectionSummaryDto(from, to, groupBy, rows,
            rows.Sum(r => r.Collected), rows.Sum(r => r.Reversed), rows.Sum(r => r.Net)));
    }

    public async Task<Result<ReconciliationDto>> ReconcileAsync(DateOnly? date, CancellationToken cancellationToken = default)
    {
        var day = date ?? clock.Today;
        var payments = await db.Payments.AsNoTracking()
            .Where(p => p.PaymentDate == day)
            .Select(p => new
            {
                p.Id, p.CashierUserId, p.Status, p.AmountDue, p.Change, p.RemittanceId,
                Allocations = p.Allocations.Sum(a => a.Amount),
                Tendered = p.Tenders.Sum(t => t.Amount),
            })
            .ToListAsync(cancellationToken);
        var remittances = await db.Remittances.AsNoTracking()
            .Where(r => r.CollectionDate == day && r.Status != RemittanceStatus.Returned)
            .Select(r => new
            {
                r.Id, r.CashierUserId, r.TotalAmount, r.RemittanceNumber,
                Items = r.Items.Sum(i => i.Amount),
                Modes = r.ModeTotals.Sum(m => m.Amount),
                Accounts = r.AccountTotals.Sum(a => a.Amount),
            })
            .ToListAsync(cancellationToken);
        var names = await NamesAsync(payments.Select(p => p.CashierUserId).Concat(remittances.Select(r => (Guid?)r.CashierUserId)), cancellationToken);

        var cashiers = payments.Select(p => p.CashierUserId).Concat(remittances.Select(r => (Guid?)r.CashierUserId)).Distinct();
        var rows = cashiers.Select(cashier =>
        {
            var mine = payments.Where(p => p.CashierUserId == cashier).ToList();
            // Voided receipts were never collected; a reversed one was collected that day and is undone on its own date.
            var counted = mine.Where(p => p.Status != PaymentStatus.Voided).ToList();
            var myRemittances = remittances.Where(r => r.CashierUserId == cashier).ToList();
            var problems = new List<string>();
            foreach (var p in mine.Where(p => p.Allocations != p.AmountDue))
            {
                problems.Add($"Receipt {p.Id}: allocation lines {p.Allocations:N2} ≠ amount {p.AmountDue:N2}.");
            }
            foreach (var p in mine.Where(p => p.Tendered - p.Change != p.AmountDue))
            {
                problems.Add($"Receipt {p.Id}: tendered less change {p.Tendered - p.Change:N2} ≠ amount {p.AmountDue:N2}.");
            }
            foreach (var r in myRemittances.Where(r => r.Items != r.TotalAmount || r.Modes != r.TotalAmount || r.Accounts != r.TotalAmount))
            {
                problems.Add($"Remittance {r.RemittanceNumber ?? r.Id.ToString()}: total {r.TotalAmount:N2}, receipts {r.Items:N2}, by mode {r.Modes:N2}, by account {r.Accounts:N2} do not agree.");
            }
            var remittedIds = myRemittances.Select(r => r.Id).ToHashSet();
            var unremitted = counted.Where(p => p.Status == PaymentStatus.Posted && p.RemittanceId is null).ToList();
            if (unremitted.Count > 0)
            {
                problems.Add($"{unremitted.Count} posted receipt(s) totalling {unremitted.Sum(p => p.AmountDue):N2} not yet remitted.");
            }
            return new ReconciliationRowDto(cashier, cashier is { } id ? names.GetValueOrDefault(id) : null, counted.Count,
                counted.Sum(p => p.AmountDue), counted.Sum(p => p.Allocations), counted.Sum(p => p.Tendered - p.Change),
                myRemittances.Sum(r => r.TotalAmount), unremitted.Sum(p => p.AmountDue), unremitted.Count,
                mine.Count(p => p.Status == PaymentStatus.Voided), problems);
        }).OrderBy(r => r.CashierName ?? r.CashierUserId?.ToString()).ToList();

        return Result.Success(new ReconciliationDto(day, rows, rows.All(r => r.Problems.Count == 0)));
    }

    // --- Helpers ---

    private sealed record PaymentRow(Guid Id, DateOnly PaymentDate, Guid? CashierUserId, decimal AmountDue, decimal Change);

    private sealed record LineRow(Guid PaymentId, decimal Amount, int TaxYear, CollectionYearCategory YearCategory, string AccountCode,
        string AccountName, string? Fund, string TaxTypeCode, string TaxTypeName, string? Barangay);

    private async Task<Dictionary<Guid, string>> NamesAsync(IEnumerable<Guid?> ids, CancellationToken ct)
    {
        var wanted = ids.Where(i => i is not null).Select(i => i!.Value).Distinct().ToList();
        return await db.AppUsers.AsNoTracking().Where(u => wanted.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.DisplayName, ct);
    }

    private IQueryable<Remittance> WithDetails() => db.Remittances.AsNoTracking()
        .Include(x => x.ModeTotals).ThenInclude(m => m.PaymentMode)
        .Include(x => x.AccountTotals)
        .Include(x => x.Items).ThenInclude(i => i.Payment);

    private async Task<RemittanceDto> MapAsync(Guid id, CancellationToken ct)
    {
        var remittance = await WithDetails().SingleAsync(x => x.Id == id, ct);
        return ToDto(remittance, await NamesAsync([remittance.CashierUserId], ct));
    }

    private static RemittanceDto ToDto(Remittance r, IReadOnlyDictionary<Guid, string> names) => new(
        r.Id, r.RemittanceNumber, r.CashierUserId, names.GetValueOrDefault(r.CashierUserId), r.CollectionDate, r.Status, r.PaymentCount, r.TotalAmount,
        r.Remarks, r.CreatedAt, r.DecidedBy, r.DecidedAt, r.DecisionRemarks,
        r.ModeTotals.OrderBy(m => m.PaymentMode!.SortOrder).ThenBy(m => m.PaymentMode!.Code)
            .Select(m => new RemittanceModeTotalDto(m.PaymentModeId, m.PaymentMode!.Code, m.PaymentMode.Name, m.Amount)).ToList(),
        r.AccountTotals.OrderBy(a => a.AccountCode).Select(a => new RemittanceAccountTotalDto(a.AccountCode, a.AccountName, a.Fund, a.Amount)).ToList(),
        r.Items.OrderBy(i => i.Payment!.ReceivedAt)
            .Select(i => new RemittanceItemDto(i.PaymentId, i.Payment!.OfficialReceiptNumber, i.Payment.PayorName, i.Amount, i.Payment.Status)).ToList());
}
