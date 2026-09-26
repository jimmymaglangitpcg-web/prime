using System.Globalization;
using Prime.Domain.Entities.Billing;
using Prime.Domain.Enums;

namespace Prime.Domain.DomainServices;

/// <summary>
/// The discount, penalty and interest steps shared by
/// <see cref="BillingCalculator"/> (charged on an installment's whole tax, as
/// if paid on the bill's as-of date) and <see cref="CollectionCalculator"/>
/// (charged on the principal a payment settles, as of the payment date) —
/// one copy of the arithmetic (CLAUDE.md Rule 9; docs/analysis/collection.md §2).
/// Each method takes the base amount explicitly and resolves the most
/// specific rule for the tax type.
/// </summary>
internal static class BillingCharges
{
    /// <summary>
    /// Discounts on <paramref name="baseAmount"/> paid on <paramref name="paidOn"/>
    /// (which must be on/before the due date — the caller checks). The
    /// advance-payment discount also needs <paramref name="paidOn"/> on/before its
    /// cutoff and <paramref name="advanceEligible"/>. Without stacking only the
    /// larger discount is given.
    /// </summary>
    public static IReadOnlyList<BillingLine> Discounts(IReadOnlyList<DiscountRule> rules, bool allowStacking, int taxYear,
        DateOnly paidOn, Guid taxTypeId, int sequence, DateOnly dueDate, decimal baseAmount, bool advanceEligible = true)
    {
        var candidates = new List<BillingLine>();

        if (MostSpecific(rules.Where(d => d.Kind == DiscountKind.PromptPayment).ToList(), d => d.TaxTypeId, taxTypeId) is { } prompt)
        {
            candidates.Add(DiscountLine(prompt, taxTypeId, sequence, dueDate, baseAmount,
                $"prompt payment: paid on/before due date {Format(dueDate)}"));
        }

        if (advanceEligible
            && MostSpecific(rules.Where(d => d.Kind == DiscountKind.AdvancePayment).ToList(), d => d.TaxTypeId, taxTypeId) is { } advance)
        {
            var cutoff = new DateOnly(taxYear + advance.CutoffYearOffset!.Value, advance.CutoffMonth!.Value, advance.CutoffDay!.Value);
            if (paidOn <= cutoff)
            {
                candidates.Add(DiscountLine(advance, taxTypeId, sequence, dueDate, baseAmount,
                    $"advance payment: whole year paid on/before {Format(cutoff)}"));
            }
        }

        if (allowStacking || candidates.Count <= 1)
        {
            return candidates;
        }
        // Largest discount = most negative amount; ties keep the prompt-payment rule (first added).
        return [candidates.OrderBy(c => c.Amount).First()];
    }

    /// <summary>
    /// The penalty on <paramref name="baseAmount"/> once more than the rule's
    /// grace days have passed since the due date. A fixed-amount penalty is
    /// charged only when <paramref name="fixedAmountAllowed"/> (a collection
    /// charges it once per installment and tax type, not once per payment).
    /// </summary>
    public static BillingLine? Penalty(IReadOnlyList<PenaltyRule> rules, DateOnly paidOn, Guid taxTypeId, int sequence,
        DateOnly dueDate, decimal baseAmount, bool fixedAmountAllowed = true)
    {
        var rule = MostSpecific(rules, r => r.TaxTypeId, taxTypeId);
        var daysOverdue = paidOn.DayNumber - dueDate.DayNumber;
        if (rule is null || daysOverdue <= rule.AppliesAfterDays)
        {
            return null;
        }

        if (rule.Rate is { } rate)
        {
            return new BillingLine(sequence, dueDate, taxTypeId, BillingComponent.Penalty, rule.Id, rate, baseAmount,
                Money(baseAmount * rate / 100m), null,
                $"{Format(rate)}% of {Format(baseAmount)} — {daysOverdue} days past due {Format(dueDate)}");
        }
        return fixedAmountAllowed
            ? new BillingLine(sequence, dueDate, taxTypeId, BillingComponent.Penalty, rule.Id, null, baseAmount,
                rule.FixedAmount!.Value, null,
                $"fixed {Format(rule.FixedAmount!.Value)} — {daysOverdue} days past due {Format(dueDate)}")
            : null;
    }

    /// <summary>Interest on <paramref name="baseAmount"/> for the months from the due date to <paramref name="paidOn"/>.</summary>
    public static BillingLine? Interest(IReadOnlyList<InterestRule> rules, DateOnly paidOn, Guid taxTypeId, int sequence,
        DateOnly dueDate, decimal baseAmount)
    {
        var rule = MostSpecific(rules, r => r.TaxTypeId, taxTypeId);
        if (rule is null)
        {
            return null;
        }
        var elapsed = BillingCalculator.DelinquentMonths(dueDate, paidOn, rule.MonthCounting);
        var months = rule.MaxMonths is { } max ? Math.Min(elapsed, max) : elapsed;
        if (months == 0)
        {
            return null;
        }

        var capped = months < elapsed ? $" (capped at {months} of {elapsed})" : "";
        return new BillingLine(sequence, dueDate, taxTypeId, BillingComponent.Interest, rule.Id, rule.RatePerMonth, baseAmount,
            Money(baseAmount * rule.RatePerMonth / 100m * months), months,
            $"{Format(rule.RatePerMonth)}%/month × {months} months{capped} on {Format(baseAmount)} past due {Format(dueDate)}");
    }

    private static BillingLine DiscountLine(DiscountRule rule, Guid taxTypeId, int sequence, DateOnly dueDate, decimal baseAmount, string reason) =>
        new(sequence, dueDate, taxTypeId, BillingComponent.Discount, rule.Id, rule.Rate, baseAmount,
            -Money(baseAmount * rule.Rate / 100m), null,
            $"{Format(rule.Rate)}% of {Format(baseAmount)} — {reason}");

    /// <summary>The rule scoped to <paramref name="taxTypeId"/> if any, else the one scoped to every tax type (null).</summary>
    public static T? MostSpecific<T>(IReadOnlyList<T> rules, Func<T, Guid?> scope, Guid taxTypeId) where T : class =>
        rules.FirstOrDefault(r => scope(r) == taxTypeId) ?? rules.FirstOrDefault(r => scope(r) is null);

    public static decimal Money(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    public static string Format(decimal value) => value.ToString("#,0.00####", CultureInfo.InvariantCulture);

    public static string Format(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
