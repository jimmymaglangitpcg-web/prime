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
/// <item>Annual tax = Σ over the assessment lines of line assessed value ×
/// the tax rate for the line's classification, each rounded. A
/// classification-specific rate wins over the general (null-classification)
/// rate; a line with neither bears none of that tax type.</item>
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

        if (input.AssessedValue < 0 || input.Lines.Any(l => l.AssessedValue < 0))
        {
            problems.Add("assessed value is negative");
        }
        if (input.Lines.Count > 0 && input.Lines.Sum(l => l.AssessedValue) != input.AssessedValue)
        {
            problems.Add("the assessed value differs from the sum of its lines");
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

        foreach (var taxTypeId in ApplicableRates(input).Select(r => r.TaxTypeId).Distinct())
        {
            var taxType = AnnualTax(input, taxTypeId, notes);
            var rate = input.TaxRates.First(r => r.Id == taxType.TaxRateId);
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

    /// <summary>
    /// One tax type's annual tax: each line taxed at the rate for its
    /// classification, summed, then any increase cap applied to the total
    /// (the cap is per tax type, never per line). The result names the
    /// principal line's rate — the line with the largest assessed value.
    /// </summary>
    private static BillingTaxTypeResult AnnualTax(BillingCalculationInput input, Guid taxTypeId, List<string> notes)
    {
        var lines = new List<BillingTaxTypeLine>();
        foreach (var line in input.EffectiveLines)
        {
            var rate = input.TaxRates.FirstOrDefault(r => r.TaxTypeId == taxTypeId && r.ClassificationId is not null && r.ClassificationId == line.ClassificationId)
                ?? input.TaxRates.FirstOrDefault(r => r.TaxTypeId == taxTypeId && r.ClassificationId is null);
            if (rate is not null)
            {
                lines.Add(new BillingTaxTypeLine(line.ClassificationId, line.AssessedValue, rate.Id, rate.Rate, Money(line.AssessedValue * rate.Rate / 100m)));
            }
        }
        var principal = lines.OrderByDescending(l => l.AssessedValue).First();
        var assessedValue = lines.Sum(l => l.AssessedValue);
        var computed = lines.Sum(l => l.Tax);

        var cap = MostSpecific(input.TaxIncreaseCapRules, r => r.TaxTypeId, taxTypeId);
        if (cap is null)
        {
            return new BillingTaxTypeResult(taxTypeId, principal.TaxRateId, principal.RatePercent, assessedValue, computed, null, null, null, computed, lines);
        }

        var baseline = input.CapBaselines.SingleOrDefault(b => b.TaxTypeId == taxTypeId && b.Baseline == cap.Baseline);
        if (baseline is null)
        {
            notes.Add($"Tax increase cap {cap.Id} was not applied to tax type {taxTypeId}: no {cap.Baseline} baseline tax was supplied (DOMAIN VERIFICATION REQUIRED — docs/BILLING.md §3.7).");
            return new BillingTaxTypeResult(taxTypeId, principal.TaxRateId, principal.RatePercent, assessedValue, computed, null, null, null, computed, lines);
        }

        var limit = Money(baseline.Amount * (1m + cap.MaxIncreasePercent / 100m));
        return new BillingTaxTypeResult(taxTypeId, principal.TaxRateId, principal.RatePercent, assessedValue, computed,
            cap.Id, baseline.Amount, limit, Math.Min(computed, limit), lines);
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

    /// <summary>The rates that apply to at least one line: general ones, and those of a line's classification.</summary>
    private static List<TaxRate> ApplicableRates(BillingCalculationInput input)
    {
        var classifications = input.EffectiveLines.Select(l => l.ClassificationId).ToHashSet();
        return input.TaxRates.Where(r => r.ClassificationId is null || classifications.Contains(r.ClassificationId)).ToList();
    }

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
