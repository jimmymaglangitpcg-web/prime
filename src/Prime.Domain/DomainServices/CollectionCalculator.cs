using Prime.Domain.Entities.Billing;
using Prime.Domain.Enums;

namespace Prime.Domain.DomainServices;

/// <summary>
/// docs/analysis/collection.md §2 — pure, deterministic allocation of a
/// payment. What is owed is one installment of one tax type of one posted
/// bill (a <see cref="CollectionKey"/>): its principal comes from the bill,
/// and the charges are computed on the payment date, on the principal being
/// paid only ("charges follow principal"), by the same
/// <see cref="BillingCharges"/> steps the bill uses, with the rules frozen on
/// the bill.
///
/// Decisions (user, 2026-09-26; each DOMAIN VERIFICATION REQUIRED):
/// <list type="bullet">
/// <item>A whole installment settles every tax type of it; a part
/// (<see cref="CollectionItem.PrincipalAmount"/>) is split over the tax types
/// in proportion to what each still owes, and the rounding remainder goes to
/// the first with room, in bill order.</item>
/// <item>Prompt-payment discount only when this payment settles the key's
/// whole principal on/before its due date, with nothing paid on it
/// before; advance-payment discount only when this one payment settles the
/// whole year of that tax type on/before the cutoff.</item>
/// <item>Past due: penalty and interest on the principal paid, interest months
/// from the due date to the payment date. A fixed-amount penalty is charged
/// once per key.</item>
/// <item>Allocations are ordered oldest tax year first, then bill order,
/// installment and tax type.</item>
/// </list>
/// Money: 2 decimals per line, <see cref="MidpointRounding.AwayFromZero"/>
/// (the billing precedent).
/// </summary>
public static class CollectionCalculator
{
    /// <summary>Why <paramref name="input"/> cannot be allocated, or null when it can.</summary>
    public static CollectionProblem? Validate(CollectionInput input)
    {
        if (input.Items.Count == 0)
        {
            return new(CollectionProblemKind.NothingSelected, "no installment was selected for payment");
        }
        foreach (var group in input.Items.GroupBy(i => (i.RpuId, i.TaxYear, i.InstallmentSequence)).Where(g => g.Count() > 1))
        {
            return new(CollectionProblemKind.DuplicateItem,
                $"installment {group.Key.InstallmentSequence} of tax year {group.Key.TaxYear} was selected more than once");
        }
        foreach (var bill in input.Bills.GroupBy(b => (b.RpuId, b.TaxYear)).Where(g => g.Count() > 1))
        {
            return new(CollectionProblemKind.InvalidBills, $"more than one bill was given for tax year {bill.Key.TaxYear} of one unit");
        }

        foreach (var item in input.Items)
        {
            var bill = input.Bills.SingleOrDefault(b => b.RpuId == item.RpuId && b.TaxYear == item.TaxYear);
            var keys = bill?.Keys.Where(k => k.InstallmentSequence == item.InstallmentSequence).ToList() ?? [];
            if (keys.Count == 0)
            {
                return new(CollectionProblemKind.UnknownInstallment,
                    $"there is no posted bill with installment {item.InstallmentSequence} for tax year {item.TaxYear} of this unit");
            }
            var outstanding = keys.Sum(k => k.Outstanding);
            if (outstanding == 0)
            {
                return new(CollectionProblemKind.AlreadySettled,
                    $"installment {item.InstallmentSequence} of tax year {item.TaxYear} is already paid");
            }
            if (item.PrincipalAmount is { } amount)
            {
                if (amount <= 0 || amount != BillingCharges.Money(amount))
                {
                    return new(CollectionProblemKind.InvalidAmount, "a partial amount must be positive, in centavos");
                }
                if (amount > outstanding)
                {
                    return new(CollectionProblemKind.InvalidAmount,
                        $"the partial amount {BillingCharges.Format(amount)} exceeds the {BillingCharges.Format(outstanding)} still owed on installment {item.InstallmentSequence} of tax year {item.TaxYear}");
                }
            }
        }
        return null;
    }

    public static CollectionResult Allocate(CollectionInput input)
    {
        if (Validate(input) is { } problem)
        {
            throw new InvalidOperationException($"Payment cannot be allocated: {problem.Message}.");
        }

        // Principal settled per key by this payment, before any charge is computed:
        // the advance discount looks at the whole year of a tax type.
        var principal = new Dictionary<(CollectionBill Bill, CollectionKey Key), decimal>();
        foreach (var item in input.Items)
        {
            var bill = input.Bills.Single(b => b.RpuId == item.RpuId && b.TaxYear == item.TaxYear);
            var keys = bill.Keys.Where(k => k.InstallmentSequence == item.InstallmentSequence && k.Outstanding > 0).ToList();
            var shares = Split(item.PrincipalAmount ?? keys.Sum(k => k.Outstanding), keys);
            for (var i = 0; i < keys.Count; i++)
            {
                if (shares[i] > 0)
                {
                    principal[(bill, keys[i])] = shares[i];
                }
            }
        }

        var allocations = new List<CollectionAllocation>();
        var ordered = principal
            .OrderBy(p => p.Key.Bill.TaxYear)
            .ThenBy(p => IndexOf(input.Bills, p.Key.Bill))
            .ThenBy(p => p.Key.Key.InstallmentSequence)
            .ThenBy(p => p.Key.Bill.Keys.ToList().IndexOf(p.Key.Key));
        foreach (var ((bill, key), paid) in ordered)
        {
            var category = YearCategory(bill.TaxYear, input.PaymentDate);
            CollectionAllocation From(BillingLine line) => new(bill.BillId, bill.RpuId, bill.TaxYear, key.InstallmentSequence,
                key.TaxTypeId, key.DueDate, line.Component, line.RuleId, line.RatePercent, line.BaseAmount, line.Amount,
                line.Months, category, line.Explanation);

            var settlesWholeKey = key.PrincipalPaid == 0 && paid == key.PrincipalOwed;
            allocations.Add(new(bill.BillId, bill.RpuId, bill.TaxYear, key.InstallmentSequence, key.TaxTypeId, key.DueDate,
                BillingComponent.Tax, key.TaxRateId, null, key.Outstanding, paid, null, category,
                paid == key.Outstanding
                    ? $"principal {BillingCharges.Format(paid)} — installment {key.InstallmentSequence} settled"
                    : $"principal {BillingCharges.Format(paid)} of {BillingCharges.Format(key.Outstanding)} still owed on installment {key.InstallmentSequence}"));

            if (input.PaymentDate <= key.DueDate)
            {
                if (settlesWholeKey)
                {
                    var wholeYear = bill.Keys.Where(k => k.TaxTypeId == key.TaxTypeId)
                        .All(k => k.PrincipalPaid == 0 && principal.GetValueOrDefault((bill, k)) == k.PrincipalOwed);
                    allocations.AddRange(BillingCharges.Discounts(bill.DiscountRules, bill.AllowDiscountStacking, bill.TaxYear,
                        input.PaymentDate, key.TaxTypeId, key.InstallmentSequence, key.DueDate, paid, advanceEligible: wholeYear)
                        .Select(From));
                }
            }
            else
            {
                if (BillingCharges.Penalty(bill.PenaltyRules, input.PaymentDate, key.TaxTypeId, key.InstallmentSequence,
                        key.DueDate, paid, fixedAmountAllowed: !key.FixedPenaltyCharged) is { } penalty)
                {
                    allocations.Add(From(penalty));
                }
                if (BillingCharges.Interest(bill.InterestRules, input.PaymentDate, key.TaxTypeId, key.InstallmentSequence,
                        key.DueDate, paid) is { } interest)
                {
                    allocations.Add(From(interest));
                }
            }
        }
        return new CollectionResult(allocations);
    }

    /// <summary>
    /// Every installment still owed on <paramref name="bills"/>, oldest tax
    /// year first — the default selection (paying everything outstanding).
    /// </summary>
    public static IReadOnlyList<CollectionItem> AllOutstanding(IReadOnlyList<CollectionBill> bills) =>
        bills.OrderBy(b => b.TaxYear).ThenBy(b => IndexOf(bills, b))
            .SelectMany(b => b.Keys.Where(k => k.Outstanding > 0).Select(k => k.InstallmentSequence).Distinct().Order()
                .Select(seq => new CollectionItem(b.RpuId, b.TaxYear, seq, null)))
            .ToList();

    public static CollectionYearCategory YearCategory(int taxYear, DateOnly paymentDate) =>
        taxYear == paymentDate.Year ? CollectionYearCategory.Current
        : taxYear < paymentDate.Year ? CollectionYearCategory.Prior
        : CollectionYearCategory.Advance;

    /// <summary>
    /// <paramref name="amount"/> over the keys in proportion to what each still
    /// owes, never above it; the rounding remainder goes to the first key with
    /// room, in order.
    /// </summary>
    private static List<decimal> Split(decimal amount, List<CollectionKey> keys)
    {
        var total = keys.Sum(k => k.Outstanding);
        if (amount == total)
        {
            return keys.Select(k => k.Outstanding).ToList();
        }
        var shares = keys.Select(k => Math.Min(k.Outstanding, BillingCharges.Money(amount * k.Outstanding / total))).ToList();
        var remainder = amount - shares.Sum();
        for (var i = 0; i < keys.Count && remainder != 0; i++)
        {
            var change = remainder > 0
                ? Math.Min(remainder, keys[i].Outstanding - shares[i])
                : Math.Max(remainder, -shares[i]);
            shares[i] += change;
            remainder -= change;
        }
        return shares;
    }

    private static int IndexOf(IReadOnlyList<CollectionBill> bills, CollectionBill bill)
    {
        for (var i = 0; i < bills.Count; i++)
        {
            if (ReferenceEquals(bills[i], bill))
            {
                return i;
            }
        }
        return -1;
    }
}
