using Microsoft.EntityFrameworkCore;
using Prime.Application.Common.Interfaces;
using Prime.Application.Features.Forms;
using Prime.Domain.Enums;

namespace Prime.Application.Features.Collection;

/// <summary>
/// The official receipt of a payment (docs/analysis/collection.md §5), carrying
/// the eOR minimum content of DOF DO 054-2024 §7.1: office and location code,
/// payor, date and time, each line's amount coded to its revenue account, OR
/// number, transaction number, mode of payment, and the bill/TD numbers as
/// the "Order of Payment / Assessment Number". Only a posted payment can be
/// issued; issuing is once per payment, so a reprint returns the frozen receipt.
/// </summary>
public sealed class PaymentFormDataProvider(IApplicationDbContext db) : IFormDataProvider
{
    public FormSubjectType SubjectType => FormSubjectType.Payment;

    public async Task<FormSubjectData?> BuildAsync(Guid subjectId, CancellationToken cancellationToken)
    {
        var p = await db.Payments.AsNoTracking()
            .Include(x => x.Tenders).ThenInclude(t => t.PaymentMode)
            .Include(x => x.Allocations).ThenInclude(a => a.TaxType)
            .Include(x => x.Allocations).ThenInclude(a => a.TaxBill!).ThenInclude(b => b.TaxDeclaration)
            .Include(x => x.Allocations).ThenInclude(a => a.TaxBill!).ThenInclude(b => b.Rpu)
            .FirstOrDefaultAsync(x => x.Id == subjectId, cancellationToken);
        if (p is null)
        {
            return null;
        }

        var pins = await db.Properties.AsNoTracking()
            .Where(x => p.Allocations.Select(a => a.PropertyId).Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.PropertyIdentificationNumber, cancellationToken);
        var replaces = p.ReplacesPaymentId is { } replacedId
            ? await db.Payments.Where(x => x.Id == replacedId).Select(x => x.OfficialReceiptNumber).FirstOrDefaultAsync(cancellationToken)
            : null;

        var data = FormData.ToJson(new
        {
            payment = new
            {
                officialReceiptNumber = p.OfficialReceiptNumber,
                transactionNumber = p.TransactionNumber,
                paymentDate = p.PaymentDate,
                receivedAt = p.ReceivedAt,
                office = p.Office,
                locationCode = p.LocationCode,
                payorName = p.PayorName,
                payorAddress = p.PayorAddress,
                amountDue = p.AmountDue,
                amountTendered = p.AmountTendered,
                change = p.Change,
                status = p.Status.ToString(),
                remarks = p.Remarks,
                replacesOfficialReceiptNumber = replaces,
            },
            // Nature of collection, coded to the revenue account (one row per account).
            accounts = p.Allocations.GroupBy(a => new { a.AccountCode, a.AccountName, a.Fund })
                .OrderBy(g => g.Min(a => a.LineNumber))
                .Select(g => new { code = g.Key.AccountCode, name = g.Key.AccountName, fund = g.Key.Fund, amount = g.Sum(a => a.Amount) }),
            lines = p.Allocations.OrderBy(a => a.LineNumber).Select(a => new
            {
                pin = pins.GetValueOrDefault(a.PropertyId),
                rpuNumber = a.TaxBill!.Rpu!.RpuNumber,
                taxDeclarationNumber = a.TaxBill.TaxDeclaration!.TaxDeclarationNumber,
                billNumber = a.TaxBill.BillNumber,
                taxYear = a.TaxYear,
                installment = a.InstallmentSequence,
                taxType = a.TaxType!.Name,
                component = a.Component.ToString(),
                yearCategory = a.YearCategory.ToString(),
                accountCode = a.AccountCode,
                explanation = a.Explanation,
                amount = a.Amount,
            }),
            references = p.Allocations.Select(a => a.TaxBill!.BillNumber ?? a.TaxBill.TaxDeclaration!.TaxDeclarationNumber).Distinct().Order(),
            tenders = p.Tenders.Select(t => new { mode = t.PaymentMode!.Name, amount = t.Amount, reference = t.Reference, bank = t.Bank, checkDate = t.CheckDate }),
        });
        var blocker = p.Status == PaymentStatus.Posted ? null : $"This payment is {p.Status}; a receipt can only be issued for a posted payment.";
        return new FormSubjectData(p.OfficialReceiptNumber, data, blocker);
    }
}
