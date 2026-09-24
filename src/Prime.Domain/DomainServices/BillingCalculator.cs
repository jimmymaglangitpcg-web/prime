using System.Globalization;
using Prime.Domain.Entities.Billing;
using Prime.Domain.Enums;

namespace Prime.Domain.DomainServices;

/// <summary>
/// docs/BILLING.md §5 — pure, deterministic billing engine (the
/// <see cref="ValuationCalculator"/> pattern). Given an assessed value and
/// the approved rules in force, it produces the bill lines as of a date. It
/// holds no rate, percentage or date of its own: every figure comes from a
/// rule the caller passes in (CLAUDE.md §7, §44).
///
/// Per tax type:
/// <list type="number">
/// <item>Annual tax = AssessedValue × TaxRate. A classification-specific
/// rate wins over the general (null-classification) rate.</item>
/// <item>If a <see cref="TaxIncreaseCapRule"/> applies and a matching
/// baseline was supplied: annual tax = min(annual tax, baseline × (1 +
/// MaxIncreasePercent/100)) — per tax type, never on the bill total.</item>
/// <item>The annual tax is split over the payment schedule's installments by
/// share; each share is rounded and the last installment takes the
/// remainder, so the installments always add up to the annual tax.</item>
/// <item>Per installment, as of <see cref="BillingCalculationInput.AsOfDate"/>:
/// not yet overdue → discounts (prompt payment on/before the due date;
/// advance payment on/before its cutoff); overdue → penalty (after its
/// grace days) and interest (by its month counting, capped at MaxMonths).
/// An overdue installment never gets a discount.</item>
/// </list>
/// Interest and penalty are computed on the installment's tax only. Nothing
/// is assumed paid — payments are Phase 9.
///
/// Money is rounded to 2 decimals per line with
/// <see cref="MidpointRounding.AwayFromZero"/> (the AssessmentService
/// precedent) — DOMAIN VERIFICATION REQUIRED (docs/BILLING.md §2).
/// </summary>
public static class BillingCalculator
{
    /// <summary>
    /// Why <paramref name="input"/> cannot be billed, or null when it can.
    /// Callers check first and report the problem; <see cref="Calculate"/>
    /// throws on the same conditions.
    /// </summary>
    public static string? Validate(BillingCalculationInput input)
    {
        var problems = new List<string>();

        if (input.AssessedValue < 0)
        {
            problems.Add("assessed value is negative");
        }

        var allRules = input.TaxRates.Cast<BillingRule>()
            .Append(input.PaymentSchedule)
            .Concat(input.DiscountRules).Concat(input.InterestRules)
            .Concat(input.PenaltyRules).Concat(input.TaxIncreaseCapRules);
        if (allRules.Any(r => r.Status != WorkflowStatus.Approved))
        {
            problems.Add("only approved rules can be applied");
        }

        var rates = ApplicableRates(input);
        if (rates.Count == 0)
        {
            problems.Add("no tax rate applies to this classification");
        }
        foreach (var group in rates.GroupBy(r => (r.TaxTypeId, r.ClassificationId)).Where(g => g.Count() > 1))
        {
            problems.Add($"more than one tax rate for tax type {group.Key.TaxTypeId} and the same classification");
        }

        var installments = input.PaymentSchedule.Installments;
        if (installments.Count == 0)
        {
            problems.Add("the payment schedule has no installments");
        }
        else if (installments.Sum(i => i.SharePercent) != 100m)
        {
            problems.Add("the payment schedule's installment shares do not total 100");
        }

        foreach (var kind in Enum.GetValues<DiscountKind>())
        {
            AddScopeConflicts(problems, $"{kind} discount rule", input.DiscountRules.Where(d => d.Kind == kind), d => d.TaxTypeId);
        }
        foreach (var discount in input.DiscountRules.Where(d => d.Kind == DiscountKind.AdvancePayment))
        {
            if (discount.CutoffMonth is null || discount.CutoffDay is null || discount.CutoffYearOffset is null)
            {
                problems.Add($"advance-payment discount rule {discount.Id} has no cutoff date");
            }
        }
        AddScopeConflicts(problems, "interest rule", input.InterestRules, r => r.TaxTypeId);
        AddScopeConflicts(problems, "penalty rule", input.PenaltyRules, r => r.TaxTypeId);
        foreach (var penalty in input.PenaltyRules)
        {
            if ((penalty.Rate is null) == (penalty.FixedAmount is null))
            {
                problems.Add($"penalty rule {penalty.Id} must have exactly one of rate or fixed amount");
            }
            else if (penalty.FixedAmount is not null && penalty.TaxTypeId is null)
            {
                // A fixed amount "for every tax type" would be charged once per tax
                // type — a reading nobody has confirmed, so it is refused, not guessed.
                problems.Add($"fixed-amount penalty rule {penalty.Id} must name a tax type");
            }
        }
        AddScopeConflicts(problems, "tax increase cap rule", input.TaxIncreaseCapRules, r => r.TaxTypeId);
        foreach (var group in input.CapBaselines.GroupBy(b => (b.TaxTypeId, b.Baseline)).Where(g => g.Count() > 1))
        {
            problems.Add($"more than one {group.Key.Baseline} baseline for tax type {group.Key.TaxTypeId}");
        }
        if (input.CapBaselines.Any(b => b.Amount < 0))
        {
            problems.Add("a cap baseline tax is negative");
        }

        return problems.Count == 0 ? null : string.Join("; ", problems);
    }

    public static BillingCalculationResult Calculate(BillingCalculationInput input, BillingCalculationOptions options)
    {
        if (Validate(input) is { } problem)
        {
            throw new InvalidOperationException($"Bill cannot be calculated: {problem}.");
        }

        var installments = input.PaymentSchedule.Installments.OrderBy(i => i.Sequence).ToList();
        var taxTypes = new List<BillingTaxTypeResult>();
        var lines = new List<BillingLine>();
        var notes = new List<string>();

        foreach (var rate in SelectRatePerTaxType(ApplicableRates(input)))
        {
            var taxType = AnnualTax(input, rate, notes);
            taxTypes.Add(taxType);

            var shares = SplitIntoInstallments(taxType.AnnualTax, installments);
            for (var i = 0; i < installments.Count; i++)
            {
                var installment = installments[i];
                var dueDate = new DateOnly(input.TaxYear, installment.DueMonth, installment.DueDay);
                var tax = shares[i];

                lines.Add(new BillingLine(installment.Sequence, dueDate, rate.TaxTypeId, BillingComponent.Tax, rate.Id,
                    rate.Rate, taxType.AnnualTax, tax, null,
                    $"{Format(installment.SharePercent)}% of annual tax {Format(taxType.AnnualTax)}"));

                if (input.AsOfDate <= dueDate)
                {
                    lines.AddRange(Discounts(input, options, rate.TaxTypeId, installment.Sequence, dueDate, tax));
                }
                else
                {
                    if (Penalty(input, rate.TaxTypeId, installment.Sequence, dueDate, tax) is { } penalty)
                    {
                        lines.Add(penalty);
                    }
                    if (Interest(input, rate.TaxTypeId, installment.Sequence, dueDate, tax) is { } interest)
                    {
                        lines.Add(interest);
                    }
                }
            }
        }

        return new BillingCalculationResult(taxTypes, lines, notes);
    }

    /// <summary>
    /// Whole months of delinquency from <paramref name="dueDate"/> to
    /// <paramref name="asOfDate"/>, counted from the due date's day of month
    /// (a due date of 31 March completes its first month on 30 April).
    /// </summary>
    public static int DelinquentMonths(DateOnly dueDate, DateOnly asOfDate, InterestMonthCounting counting)
    {
        if (asOfDate <= dueDate)
        {
            return 0;
        }
        var completed = (asOfDate.Year - dueDate.Year) * 12 + asOfDate.Month - dueDate.Month;
        if (dueDate.AddMonths(completed) > asOfDate)
        {
            completed--;
        }
        var partial = dueDate.AddMonths(completed) < asOfDate;
        return counting == InterestMonthCounting.FractionCountsAsFullMonth && partial ? completed + 1 : completed;
    }

    private static BillingTaxTypeResult AnnualTax(BillingCalculationInput input, TaxRate rate, List<string> notes)
    {
        var computed = Money(input.AssessedValue * rate.Rate / 100m);
        var cap = MostSpecific(input.TaxIncreaseCapRules, r => r.TaxTypeId, rate.TaxTypeId);
        if (cap is null)
        {
            return new BillingTaxTypeResult(rate.TaxTypeId, rate.Id, rate.Rate, input.AssessedValue, computed, null, null, null, computed);
        }

        var baseline = input.CapBaselines.SingleOrDefault(b => b.TaxTypeId == rate.TaxTypeId && b.Baseline == cap.Baseline);
        if (baseline is null)
        {
            notes.Add($"Tax increase cap {cap.Id} was not applied to tax type {rate.TaxTypeId}: no {cap.Baseline} baseline tax was supplied (DOMAIN VERIFICATION REQUIRED — docs/BILLING.md §3.7).");
            return new BillingTaxTypeResult(rate.TaxTypeId, rate.Id, rate.Rate, input.AssessedValue, computed, null, null, null, computed);
        }

        var limit = Money(baseline.Amount * (1m + cap.MaxIncreasePercent / 100m));
        return new BillingTaxTypeResult(rate.TaxTypeId, rate.Id, rate.Rate, input.AssessedValue, computed,
            cap.Id, baseline.Amount, limit, Math.Min(computed, limit));
    }

    private static List<decimal> SplitIntoInstallments(decimal annualTax, List<PaymentScheduleInstallment> installments)
    {
        var shares = new List<decimal>(installments.Count);
        for (var i = 0; i < installments.Count - 1; i++)
        {
            shares.Add(Money(annualTax * installments[i].SharePercent / 100m));
        }
        shares.Add(annualTax - shares.Sum());
        return shares;
    }

    private static IEnumerable<BillingLine> Discounts(BillingCalculationInput input, BillingCalculationOptions options,
        Guid taxTypeId, int sequence, DateOnly dueDate, decimal tax)
    {
        var candidates = new List<BillingLine>();

        if (MostSpecific(input.DiscountRules.Where(d => d.Kind == DiscountKind.PromptPayment).ToList(), d => d.TaxTypeId, taxTypeId) is { } prompt)
        {
            candidates.Add(DiscountLine(prompt, taxTypeId, sequence, dueDate, tax,
                $"prompt payment: paid on/before due date {Format(dueDate)}"));
        }

        if (MostSpecific(input.DiscountRules.Where(d => d.Kind == DiscountKind.AdvancePayment).ToList(), d => d.TaxTypeId, taxTypeId) is { } advance)
        {
            var cutoff = new DateOnly(input.TaxYear + advance.CutoffYearOffset!.Value, advance.CutoffMonth!.Value, advance.CutoffDay!.Value);
            if (input.AsOfDate <= cutoff)
            {
                candidates.Add(DiscountLine(advance, taxTypeId, sequence, dueDate, tax,
                    $"advance payment: whole year paid on/before {Format(cutoff)}"));
            }
        }

        if (options.AllowDiscountStacking || candidates.Count <= 1)
        {
            return candidates;
        }
        // Largest discount = most negative amount; ties keep the prompt-payment rule (first added).
        return [candidates.OrderBy(c => c.Amount).First()];
    }

    private static BillingLine DiscountLine(DiscountRule rule, Guid taxTypeId, int sequence, DateOnly dueDate, decimal tax, string reason) =>
        new(sequence, dueDate, taxTypeId, BillingComponent.Discount, rule.Id, rule.Rate, tax,
            -Money(tax * rule.Rate / 100m), null,
            $"{Format(rule.Rate)}% of {Format(tax)} — {reason}");

    private static BillingLine? Penalty(BillingCalculationInput input, Guid taxTypeId, int sequence, DateOnly dueDate, decimal tax)
    {
        var rule = MostSpecific(input.PenaltyRules, r => r.TaxTypeId, taxTypeId);
        var daysOverdue = input.AsOfDate.DayNumber - dueDate.DayNumber;
        if (rule is null || daysOverdue <= rule.AppliesAfterDays)
        {
            return null;
        }

        return rule.Rate is { } rate
            ? new BillingLine(sequence, dueDate, taxTypeId, BillingComponent.Penalty, rule.Id, rate, tax,
                Money(tax * rate / 100m), null,
                $"{Format(rate)}% of {Format(tax)} — {daysOverdue} days past due {Format(dueDate)}")
            : new BillingLine(sequence, dueDate, taxTypeId, BillingComponent.Penalty, rule.Id, null, tax,
                rule.FixedAmount!.Value, null,
                $"fixed {Format(rule.FixedAmount!.Value)} — {daysOverdue} days past due {Format(dueDate)}");
    }

    private static BillingLine? Interest(BillingCalculationInput input, Guid taxTypeId, int sequence, DateOnly dueDate, decimal tax)
    {
        var rule = MostSpecific(input.InterestRules, r => r.TaxTypeId, taxTypeId);
        if (rule is null)
        {
            return null;
        }
        var elapsed = DelinquentMonths(dueDate, input.AsOfDate, rule.MonthCounting);
        var months = rule.MaxMonths is { } max ? Math.Min(elapsed, max) : elapsed;
        if (months == 0)
        {
            return null;
        }

        var capped = months < elapsed ? $" (capped at {months} of {elapsed})" : "";
        return new BillingLine(sequence, dueDate, taxTypeId, BillingComponent.Interest, rule.Id, rule.RatePerMonth, tax,
            Money(tax * rule.RatePerMonth / 100m * months), months,
            $"{Format(rule.RatePerMonth)}%/month × {months} months{capped} on {Format(tax)} past due {Format(dueDate)}");
    }

    private static List<TaxRate> ApplicableRates(BillingCalculationInput input) =>
        input.TaxRates.Where(r => r.ClassificationId is null || r.ClassificationId == input.ClassificationId).ToList();

    /// <summary>Per tax type, the classification-specific rate if any, else the general one; first-seen tax type order.</summary>
    private static IEnumerable<TaxRate> SelectRatePerTaxType(List<TaxRate> rates) =>
        rates.GroupBy(r => r.TaxTypeId).Select(g => g.FirstOrDefault(r => r.ClassificationId is not null) ?? g.First());

    /// <summary>The rule scoped to <paramref name="taxTypeId"/> if any, else the one scoped to every tax type (null).</summary>
    private static T? MostSpecific<T>(IReadOnlyList<T> rules, Func<T, Guid?> scope, Guid taxTypeId) where T : class =>
        rules.FirstOrDefault(r => scope(r) == taxTypeId) ?? rules.FirstOrDefault(r => scope(r) is null);

    private static void AddScopeConflicts<T>(List<string> problems, string name, IEnumerable<T> rules, Func<T, Guid?> scope)
    {
        foreach (var group in rules.GroupBy(scope).Where(g => g.Count() > 1))
        {
            problems.Add($"more than one {name} for {(group.Key is { } id ? $"tax type {id}" : "all tax types")}");
        }
    }

    private static decimal Money(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private static string Format(decimal value) => value.ToString("#,0.00####", CultureInfo.InvariantCulture);

    private static string Format(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
