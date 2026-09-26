using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Prime.Application.Common;
using Prime.Application.Common.Interfaces;
using Prime.Application.Features.Numbering;
using Prime.Domain.DomainServices;
using Prime.Domain.Entities.Billing;
using Prime.Domain.Enums;

namespace Prime.Application.Features.Billing.Bills;

/// <summary>
/// Resolves what a bill needs — the posted assessment, the current Tax
/// Declaration, the approved rules in force, cap baselines — then lets
/// <see cref="BillingCalculator"/> compute it and saves the result
/// (docs/BILLING.md §4–§5, §6.1). The service does no arithmetic itself
/// (CLAUDE.md Rule 9).
/// </summary>
public sealed class BillService(
    IApplicationDbContext db,
    IValidator<GenerateBillRequest> validator,
    ICurrentUserService currentUser,
    IOptions<BillingOptions> options,
    INumberingService numbering,
    ICollectionLock collectionLock,
    IClock clock) : IBillService
{
    /// <summary>
    /// docs/BILLING.md §6.1: rules and the assessment are taken as in force on
    /// 1 January of the tax year (LGC §221 January-1 effectivity). Changeable
    /// here alone.
    /// </summary>
    public static DateOnly RulesAsOfDate(int taxYear) => new(taxYear, 1, 1);

    public async Task<Result<TaxBillDto>> GenerateAsync(GenerateBillRequest request, CancellationToken cancellationToken = default)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure<TaxBillDto>("VALIDATION_FAILED", string.Join("; ", validation.Errors.Select(e => e.ErrorMessage)));
        }

        var rpu = await db.RealPropertyUnits.FirstOrDefaultAsync(x => x.Id == request.RpuId, cancellationToken);
        if (rpu is null)
        {
            return Result.Failure<TaxBillDto>("RPU_NOT_FOUND", "No RPU was found with the given id.");
        }

        var rulesAsOf = RulesAsOfDate(request.TaxYear);
        var assessment = await db.Assessments.Include(x => x.Valuation).Include(x => x.Lines)
            .Where(x => x.RpuId == rpu.Id && x.Status == WorkflowStatus.Posted && x.EffectiveDate <= rulesAsOf)
            .OrderByDescending(x => x.EffectiveDate).ThenByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (assessment is null)
        {
            return Result.Failure<TaxBillDto>("BILL_ASSESSMENT_NOT_FOUND",
                $"This RPU has no posted assessment effective on or before {rulesAsOf:yyyy-MM-dd}.");
        }

        var taxDeclaration = await TaxDeclarationLookup.GetCurrentAsync(db, rpu.Id, cancellationToken);
        if (taxDeclaration is null)
        {
            return Result.Failure<TaxBillDto>("TAX_DECLARATION_NOT_FOUND", "A Tax Declaration is required before this RPU can be billed.");
        }
        if (taxDeclaration.Taxability != Taxability.Taxable)
        {
            return Result.Failure<TaxBillDto>("BILL_PROPERTY_EXEMPT", "The current Tax Declaration is not taxable; exemptions are outside billing.");
        }

        if (await db.TaxBills.AnyAsync(x => x.RpuId == rpu.Id && x.TaxYear == request.TaxYear
                && x.AsOfDate == request.AsOfDate && x.Status != WorkflowStatus.Cancelled, cancellationToken))
        {
            return Result.Failure<TaxBillDto>("BILL_DUPLICATE",
                "A bill for this RPU, tax year and as-of date already exists. Cancel it first to recompute.");
        }

        var schedules = await InForce(db.PaymentSchedules.Include(x => x.Installments), rulesAsOf).ToListAsync(cancellationToken);
        if (schedules.Count != 1)
        {
            return Result.Failure<TaxBillDto>("BILL_PAYMENT_SCHEDULE_NOT_FOUND",
                $"Exactly one approved payment schedule must be in force on {rulesAsOf:yyyy-MM-dd}; found {schedules.Count}.");
        }

        var smvId = assessment.Valuation?.SmvId;
        List<TaxIncreaseCapRule> caps = smvId is null
            ? []
            : await InForce(db.TaxIncreaseCapRules.Include(x => x.Smv), rulesAsOf).Where(x => x.SmvId == smvId).ToListAsync(cancellationToken);

        // Each assessment line is taxed at the rate for its own classification (docs/analysis/mrpaao-forms-model.md §8.4).
        var principal = assessment.Lines.OrderByDescending(l => l.MarketValue).ThenBy(l => l.Sequence).First();
        var input = new BillingCalculationInput
        {
            AssessedValue = assessment.AssessedValue,
            ClassificationId = principal.ClassificationId,
            Lines = assessment.Lines.OrderBy(l => l.Sequence).Select(l => new BillingAssessmentLine(l.ClassificationId, l.AssessedValue)).ToList(),
            TaxYear = request.TaxYear,
            AsOfDate = request.AsOfDate,
            // Tax types are billed in their configured order (the calculator keeps input order).
            TaxRates = await InForce(db.TaxRates, rulesAsOf)
                .OrderBy(x => x.TaxType!.SortOrder).ThenBy(x => x.TaxType!.Code).ToListAsync(cancellationToken),
            PaymentSchedule = schedules[0],
            DiscountRules = await InForce(db.DiscountRules, rulesAsOf).ToListAsync(cancellationToken),
            InterestRules = await InForce(db.InterestRules, rulesAsOf).ToListAsync(cancellationToken),
            PenaltyRules = await InForce(db.PenaltyRules, rulesAsOf).ToListAsync(cancellationToken),
            TaxIncreaseCapRules = caps,
            CapBaselines = await CapBaselinesAsync(rpu.Id, request.TaxYear, caps, cancellationToken),
        };
        if (BillingCalculator.Validate(input) is { } problem)
        {
            return Result.Failure<TaxBillDto>("BILL_RULES_INVALID", $"The billing rules in force on {rulesAsOf:yyyy-MM-dd} cannot produce a bill: {problem}.");
        }

        var stacking = options.Value.AllowDiscountStacking;
        var result = BillingCalculator.Calculate(input, new BillingCalculationOptions(stacking));

        var bill = new TaxBill
        {
            PropertyId = rpu.PropertyId,
            RpuId = rpu.Id,
            TaxDeclarationId = taxDeclaration.Id,
            AssessmentId = assessment.Id,
            TaxYear = request.TaxYear,
            AsOfDate = request.AsOfDate,
            RulesAsOfDate = rulesAsOf,
            AssessedValue = assessment.AssessedValue,
            ClassificationId = principal.ClassificationId,
            DiscountStackingAllowed = stacking,
            Notes = result.Notes.Count == 0 ? null : string.Join(Environment.NewLine, result.Notes),
            TaxTypes = result.TaxTypes.Select(t => new TaxBillTaxType
            {
                TaxTypeId = t.TaxTypeId,
                TaxRateId = t.TaxRateId,
                RatePercent = t.RatePercent,
                ComputedAnnualTax = t.ComputedAnnualTax,
                CapRuleId = t.CapRuleId,
                CapBaselineTax = t.CapBaselineTax,
                CapLimit = t.CapLimit,
                AnnualTax = t.AnnualTax,
                Lines = t.Lines.Select(l => new TaxBillTaxTypeLine
                {
                    ClassificationId = l.ClassificationId, AssessedValue = l.AssessedValue, TaxRateId = l.TaxRateId, RatePercent = l.RatePercent, Tax = l.Tax,
                }).ToList(),
            }).ToList(),
            Details = result.Lines.Select((l, i) => new TaxBillDetail
            {
                LineNumber = i + 1,
                InstallmentSequence = l.InstallmentSequence,
                DueDate = l.DueDate,
                TaxTypeId = l.TaxTypeId,
                Component = l.Component,
                RuleId = l.RuleId,
                RatePercent = l.RatePercent,
                BaseAmount = l.BaseAmount,
                Amount = l.Amount,
                Months = l.Months,
                Explanation = l.Explanation,
            }).ToList(),
        };

        db.TaxBills.Add(bill);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // UX_TaxBills_Rpu_TaxYear_AsOf_Live: the same bill generated concurrently.
            return Result.Failure<TaxBillDto>("BILL_DUPLICATE",
                "A bill for this RPU, tax year and as-of date was created at the same time. Reload and try again.");
        }

        return Result.Success(await MapAsync(bill.Id, cancellationToken));
    }

    public async Task<Result<TaxBillDto>> PostAsync(Guid billId, CancellationToken cancellationToken = default)
    {
        var bill = await db.TaxBills.FirstOrDefaultAsync(x => x.Id == billId, cancellationToken);
        if (bill is null)
        {
            return NotFound();
        }
        if (bill.Status != WorkflowStatus.Draft)
        {
            return Result.Failure<TaxBillDto>("BILL_NOT_DRAFT", "Only a Draft bill can be posted.");
        }

        var previous = await db.TaxBills.FirstOrDefaultAsync(x => x.RpuId == bill.RpuId && x.TaxYear == bill.TaxYear
            && x.Status == WorkflowStatus.Posted, cancellationToken);
        var now = DateTimeOffset.UtcNow;

        // Cancel the superseded bill before posting: the one-posted-bill-per-year
        // unique index is not deferrable.
        var ownsTransaction = db.Database.CurrentTransaction is null;
        var transaction = ownsTransaction ? await db.Database.BeginTransactionAsync(cancellationToken) : null;
        try
        {
            // What is owed changes: no payment of this unit and year may be allocated meanwhile.
            await collectionLock.LockAsync([(bill.RpuId, bill.TaxYear)], cancellationToken);
            if (previous is not null)
            {
                previous.Status = WorkflowStatus.Cancelled;
                previous.CancelledAt = now;
                previous.CancelledBy = currentUser.AppUserId;
                previous.CancellationReason = $"Superseded by bill {bill.Id}.";
                previous.SupersededByBillId = bill.Id;
                await db.SaveChangesAsync(cancellationToken);
            }

            // Bill number from the TaxBill scheme in force, if any ({YEAR} = the tax year).
            var context = await NumberContexts.ForPropertyAsync(db, bill.PropertyId, bill.TaxYear, cancellationToken);
            var number = await numbering.GenerateIfConfiguredAsync(NumberedDocumentKind.TaxBill, context, clock.Today, cancellationToken);
            if (number.IsFailure)
            {
                return Result.Failure<TaxBillDto>(number.Code!, number.Message!);
            }
            bill.BillNumber = number.Value;
            bill.Status = WorkflowStatus.Posted;
            bill.PostedAt = now;
            bill.PostedBy = currentUser.AppUserId;
            await db.SaveChangesAsync(cancellationToken);

            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }
        }
        catch (DbUpdateException)
        {
            return Result.Failure<TaxBillDto>("BILL_POST_CONFLICT",
                "Another bill for this RPU and tax year was posted at the same time. Nothing was changed; reload and try again.");
        }
        finally
        {
            if (transaction is not null)
            {
                await transaction.DisposeAsync();
            }
        }

        return Result.Success(await MapAsync(bill.Id, cancellationToken));
    }

    public async Task<Result<TaxBillDto>> CancelAsync(Guid billId, string reason, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > 1000)
        {
            return Result.Failure<TaxBillDto>("VALIDATION_FAILED", "A cancellation reason is required (max 1000).");
        }
        var bill = await db.TaxBills.FirstOrDefaultAsync(x => x.Id == billId, cancellationToken);
        if (bill is null)
        {
            return NotFound();
        }
        if (bill.Status == WorkflowStatus.Cancelled)
        {
            return Result.Failure<TaxBillDto>("BILL_ALREADY_CANCELLED", "The bill is already cancelled.");
        }

        var ownsTransaction = db.Database.CurrentTransaction is null;
        var transaction = ownsTransaction ? await db.Database.BeginTransactionAsync(cancellationToken) : null;
        try
        {
            // docs/analysis/collection.md §3: payments follow the installment, so a posted
            // bill they settle may be superseded by a new bill, but not simply cancelled.
            await collectionLock.LockAsync([(bill.RpuId, bill.TaxYear)], cancellationToken);
            if (bill.Status == WorkflowStatus.Posted && await db.PaymentAllocations.AnyAsync(a =>
                    a.RpuId == bill.RpuId && a.TaxYear == bill.TaxYear
                    && db.Payments.Any(p => p.Id == a.PaymentId && p.Status == PaymentStatus.Posted), cancellationToken))
            {
                return Result.Failure<TaxBillDto>("BILL_HAS_PAYMENTS",
                    "Payments stand against this unit and tax year. Post a recomputed bill to replace this one instead of cancelling it.");
            }

            bill.Status = WorkflowStatus.Cancelled;
            bill.CancelledAt = clock.UtcNow;
            bill.CancelledBy = currentUser.AppUserId;
            bill.CancellationReason = reason;
            currentUser.Reason = reason;
            await db.SaveChangesAsync(cancellationToken);
            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }
        }
        finally
        {
            if (transaction is not null)
            {
                await transaction.DisposeAsync();
            }
        }

        return Result.Success(await MapAsync(bill.Id, cancellationToken));
    }

    public async Task<Result<TaxBillDto>> GetByIdAsync(Guid billId, CancellationToken cancellationToken = default) =>
        await db.TaxBills.AnyAsync(x => x.Id == billId, cancellationToken)
            ? Result.Success(await MapAsync(billId, cancellationToken))
            : NotFound();

    public async Task<Result<IReadOnlyList<TaxBillDto>>> ListByPropertyAsync(Guid propertyId, CancellationToken cancellationToken = default)
    {
        var bills = await WithDetails().Where(x => x.PropertyId == propertyId)
            .OrderByDescending(x => x.TaxYear).ThenByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
        return Result.Success<IReadOnlyList<TaxBillDto>>(bills.Select(ToDto).ToList());
    }

    public async Task<Result<StatementOfAccountDto>> GetStatementOfAccountAsync(Guid propertyId, CancellationToken cancellationToken = default)
    {
        var property = await db.Properties.FirstOrDefaultAsync(x => x.Id == propertyId, cancellationToken);
        if (property is null)
        {
            return Result.Failure<StatementOfAccountDto>("PROPERTY_NOT_FOUND", "No property was found with the given id.");
        }

        var bills = await WithDetails().Where(x => x.PropertyId == propertyId && x.Status == WorkflowStatus.Posted)
            .OrderBy(x => x.TaxYear).ThenBy(x => x.Rpu!.RpuNumber)
            .ToListAsync(cancellationToken);
        var lines = bills.Select(b =>
        {
            decimal Sum(BillingComponent c) => b.Details.Where(d => d.Component == c).Sum(d => d.Amount);
            return new StatementLineDto(b.Id, b.RpuId, b.Rpu!.RpuNumber, b.TaxDeclaration!.TaxDeclarationNumber, b.TaxYear, b.AsOfDate,
                b.AssessedValue, Sum(BillingComponent.Tax), Sum(BillingComponent.Discount), Sum(BillingComponent.Penalty),
                Sum(BillingComponent.Interest), b.Details.Sum(d => d.Amount));
        }).ToList();

        return Result.Success(new StatementOfAccountDto(property.Id, property.PropertyIdentificationNumber, DateTimeOffset.UtcNow,
            lines, lines.Sum(l => l.Total)));
    }

    /// <summary>
    /// Baseline tax per tax type, taken from this RPU's own posted bills
    /// (docs/BILLING.md §6.1): TaxBeforeSmv → the latest tax year before the
    /// capped SMV took effect; PreviousTaxYear → the year before this one.
    /// No such bill → no baseline, and the calculator notes the cap as not applied.
    /// </summary>
    private async Task<List<CapBaselineTax>> CapBaselinesAsync(Guid rpuId, int taxYear, List<TaxIncreaseCapRule> caps, CancellationToken ct)
    {
        var baselines = new List<CapBaselineTax>();
        foreach (var kind in caps.Select(c => c.Baseline).Distinct())
        {
            // Every cap passed in belongs to the assessment's SMV.
            var smvYear = caps[0].Smv!.EffectivityDate.Year;
            var query = db.TaxBills.Include(x => x.TaxTypes)
                .Where(x => x.RpuId == rpuId && x.Status == WorkflowStatus.Posted);
            query = kind switch
            {
                TaxIncreaseCapBaseline.TaxBeforeSmv => query.Where(x => x.TaxYear < smvYear),
                TaxIncreaseCapBaseline.PreviousTaxYear => query.Where(x => x.TaxYear == taxYear - 1),
                _ => throw new InvalidOperationException($"Unhandled {nameof(TaxIncreaseCapBaseline)}: {kind}"),
            };
            var bill = await query.OrderByDescending(x => x.TaxYear).FirstOrDefaultAsync(ct);
            if (bill is not null)
            {
                baselines.AddRange(bill.TaxTypes.Select(t => new CapBaselineTax(t.TaxTypeId, kind, t.AnnualTax)));
            }
        }
        return baselines;
    }

    private static IQueryable<T> InForce<T>(IQueryable<T> query, DateOnly date) where T : BillingRule =>
        query.Where(x => x.Status == WorkflowStatus.Approved && x.EffectiveDate <= date && (x.EndDate == null || x.EndDate >= date));

    private IQueryable<TaxBill> WithDetails() => db.TaxBills
        .Include(x => x.Rpu).Include(x => x.TaxDeclaration)
        .Include(x => x.TaxTypes).ThenInclude(t => t.TaxType)
        .Include(x => x.TaxTypes).ThenInclude(t => t.Lines)
        .Include(x => x.Details).ThenInclude(d => d.TaxType);

    private async Task<TaxBillDto> MapAsync(Guid billId, CancellationToken ct) =>
        ToDto(await WithDetails().AsNoTracking().SingleAsync(x => x.Id == billId, ct));

    private static Result<TaxBillDto> NotFound() => Result.Failure<TaxBillDto>("BILL_NOT_FOUND", "No tax bill was found with the given id.");

    private static TaxBillDto ToDto(TaxBill b) => new(
        b.Id, b.PropertyId, b.RpuId, b.Rpu!.RpuNumber, b.TaxDeclarationId, b.TaxDeclaration!.TaxDeclarationNumber, b.AssessmentId,
        b.BillNumber, b.TaxYear, b.AsOfDate, b.RulesAsOfDate, b.AssessedValue, b.ClassificationId, b.DiscountStackingAllowed, b.Notes,
        b.Status, b.CreatedAt, b.CreatedBy, b.PostedAt, b.PostedBy, b.CancelledAt, b.CancellationReason, b.SupersededByBillId,
        b.Details.Sum(d => d.Amount),
        b.TaxTypes.OrderBy(t => t.TaxType!.SortOrder).ThenBy(t => t.TaxType!.Code).Select(t => new TaxBillTaxTypeDto(t.TaxTypeId, t.TaxType!.Code, t.TaxType.Name, t.TaxRateId, t.RatePercent,
            t.ComputedAnnualTax, t.CapRuleId, t.CapBaselineTax, t.CapLimit, t.AnnualTax,
            t.Lines.OrderByDescending(l => l.AssessedValue).ThenBy(l => l.Tax).Select(l => new TaxBillTaxTypeLineDto(l.ClassificationId, l.AssessedValue, l.TaxRateId, l.RatePercent, l.Tax)).ToList())).ToList(),
        b.Details.OrderBy(d => d.LineNumber).Select(d => new TaxBillDetailDto(d.LineNumber, d.InstallmentSequence, d.DueDate,
            d.TaxTypeId, d.TaxType!.Code, d.Component, d.RuleId, d.RatePercent, d.BaseAmount, d.Amount, d.Months, d.Explanation)).ToList());
}
