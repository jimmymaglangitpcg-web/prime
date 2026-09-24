using System.Linq.Expressions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Prime.Application.Common;
using Prime.Application.Common.Interfaces;
using Prime.Domain.Entities.Billing;
using Prime.Domain.Enums;

namespace Prime.Application.Features.Billing.Rules;

/// <summary>
/// Create / approve / list for every billing rule type (docs/BILLING.md §3).
/// One generic lifecycle so maker-checker and supersession behave the same
/// for all rules:
/// <list type="bullet">
/// <item>Create → Draft. Nothing already in force changes.</item>
/// <item>Approve → the creator may not approve (CLAUDE.md §46); the rule
/// must start after every approved rule of the same scope; the currently
/// open approved rule of that scope is closed (EndDate = day before) in the
/// same transaction. A filtered unique index enforces one open approved
/// rule per scope in the database too.</item>
/// </list>
/// </summary>
public sealed class BillingRuleService(
    IApplicationDbContext db,
    ICurrentUserService currentUser,
    IValidator<CreateTaxRateRequest> taxRateValidator,
    IValidator<CreatePaymentScheduleRequest> scheduleValidator,
    IValidator<CreateDiscountRuleRequest> discountValidator,
    IValidator<CreateInterestRuleRequest> interestValidator,
    IValidator<CreatePenaltyRuleRequest> penaltyValidator,
    IValidator<CreateTaxIncreaseCapRuleRequest> capValidator) : IBillingRuleService
{
    // ------------------------------------------------------------- tax rates

    public async Task<Result<TaxRateDto>> CreateTaxRateAsync(CreateTaxRateRequest request, CancellationToken cancellationToken = default)
    {
        var invalid = await ValidateAsync<TaxRateDto, CreateTaxRateRequest>(taxRateValidator, request, cancellationToken);
        if (invalid is not null) return invalid;
        var refs = await CheckTaxTypeAsync<TaxRateDto>(request.TaxTypeId, cancellationToken);
        if (refs is not null) return refs;
        if (request.ClassificationId is { } classificationId && !await db.Classifications.AnyAsync(c => c.Id == classificationId, cancellationToken))
        {
            return Result.Failure<TaxRateDto>("CLASSIFICATION_NOT_FOUND", "The specified classification does not exist.");
        }

        var rule = Stamp(new TaxRate { TaxTypeId = request.TaxTypeId, ClassificationId = request.ClassificationId, Rate = request.Rate }, request);
        return await CreateAsync(db.TaxRates, rule, ScopeOf(rule), MapTaxRateAsync, cancellationToken);
    }

    public Task<Result<TaxRateDto>> ApproveTaxRateAsync(Guid id, CancellationToken cancellationToken = default) =>
        ApproveAsync(db.TaxRates, id, ScopeOf, MapTaxRateAsync, cancellationToken);

    public Task<Result<TaxRateDto>> GetTaxRateAsync(Guid id, CancellationToken cancellationToken = default) =>
        GetAsync(db.TaxRates, id, MapTaxRateAsync, cancellationToken);

    public async Task<Result<IReadOnlyList<TaxRateDto>>> ListTaxRatesAsync(DateOnly? asOf, CancellationToken cancellationToken = default) =>
        Result.Success<IReadOnlyList<TaxRateDto>>(
            (await Filter(db.TaxRates.Include(x => x.TaxType).Include(x => x.Classification), asOf).ToListAsync(cancellationToken)).Select(ToDto).ToList());

    private static Expression<Func<TaxRate, bool>> ScopeOf(TaxRate r) => x => x.TaxTypeId == r.TaxTypeId && x.ClassificationId == r.ClassificationId;

    private async Task<TaxRateDto> MapTaxRateAsync(Guid id, CancellationToken ct) =>
        ToDto(await db.TaxRates.Include(x => x.TaxType).Include(x => x.Classification).SingleAsync(x => x.Id == id, ct));

    private static TaxRateDto ToDto(TaxRate x) =>
        new(Header(x), TaxTypeRef(x.TaxType)!, x.ClassificationId, x.Classification?.Name, x.Rate);

    // ------------------------------------------------------ payment schedules

    public async Task<Result<PaymentScheduleDto>> CreatePaymentScheduleAsync(CreatePaymentScheduleRequest request, CancellationToken cancellationToken = default)
    {
        var invalid = await ValidateAsync<PaymentScheduleDto, CreatePaymentScheduleRequest>(scheduleValidator, request, cancellationToken);
        if (invalid is not null) return invalid;

        var rule = Stamp(new PaymentSchedule
        {
            Installments = request.Installments.OrderBy(i => i.Sequence).Select(i => new PaymentScheduleInstallment
            {
                Sequence = i.Sequence, DueMonth = i.DueMonth, DueDay = i.DueDay, SharePercent = i.SharePercent,
            }).ToList(),
        }, request);
        return await CreateAsync(db.PaymentSchedules, rule, ScopeOf(rule), MapScheduleAsync, cancellationToken);
    }

    public Task<Result<PaymentScheduleDto>> ApprovePaymentScheduleAsync(Guid id, CancellationToken cancellationToken = default) =>
        ApproveAsync(db.PaymentSchedules, id, ScopeOf, MapScheduleAsync, cancellationToken);

    public Task<Result<PaymentScheduleDto>> GetPaymentScheduleAsync(Guid id, CancellationToken cancellationToken = default) =>
        GetAsync(db.PaymentSchedules, id, MapScheduleAsync, cancellationToken);

    public async Task<Result<IReadOnlyList<PaymentScheduleDto>>> ListPaymentSchedulesAsync(DateOnly? asOf, CancellationToken cancellationToken = default) =>
        Result.Success<IReadOnlyList<PaymentScheduleDto>>(
            (await Filter(db.PaymentSchedules.Include(x => x.Installments), asOf).ToListAsync(cancellationToken)).Select(ToDto).ToList());

    private static Expression<Func<PaymentSchedule, bool>> ScopeOf(PaymentSchedule _) => x => true;

    private async Task<PaymentScheduleDto> MapScheduleAsync(Guid id, CancellationToken ct) =>
        ToDto(await db.PaymentSchedules.Include(x => x.Installments).SingleAsync(x => x.Id == id, ct));

    private static PaymentScheduleDto ToDto(PaymentSchedule x) =>
        new(Header(x), x.Installments.OrderBy(i => i.Sequence).Select(i => new InstallmentDto(i.Sequence, i.DueMonth, i.DueDay, i.SharePercent)).ToList());

    // --------------------------------------------------------- discount rules

    public async Task<Result<DiscountRuleDto>> CreateDiscountRuleAsync(CreateDiscountRuleRequest request, CancellationToken cancellationToken = default)
    {
        var invalid = await ValidateAsync<DiscountRuleDto, CreateDiscountRuleRequest>(discountValidator, request, cancellationToken);
        if (invalid is not null) return invalid;
        var refs = await CheckTaxTypeAsync<DiscountRuleDto>(request.TaxTypeId, cancellationToken);
        if (refs is not null) return refs;

        var rule = Stamp(new DiscountRule
        {
            TaxTypeId = request.TaxTypeId, Kind = request.Kind, Rate = request.Rate,
            CutoffMonth = request.CutoffMonth, CutoffDay = request.CutoffDay, CutoffYearOffset = request.CutoffYearOffset,
        }, request);
        return await CreateAsync(db.DiscountRules, rule, ScopeOf(rule), MapDiscountAsync, cancellationToken);
    }

    public Task<Result<DiscountRuleDto>> ApproveDiscountRuleAsync(Guid id, CancellationToken cancellationToken = default) =>
        ApproveAsync(db.DiscountRules, id, ScopeOf, MapDiscountAsync, cancellationToken);

    public Task<Result<DiscountRuleDto>> GetDiscountRuleAsync(Guid id, CancellationToken cancellationToken = default) =>
        GetAsync(db.DiscountRules, id, MapDiscountAsync, cancellationToken);

    public async Task<Result<IReadOnlyList<DiscountRuleDto>>> ListDiscountRulesAsync(DateOnly? asOf, CancellationToken cancellationToken = default) =>
        Result.Success<IReadOnlyList<DiscountRuleDto>>(
            (await Filter(db.DiscountRules.Include(x => x.TaxType), asOf).ToListAsync(cancellationToken)).Select(ToDto).ToList());

    private static Expression<Func<DiscountRule, bool>> ScopeOf(DiscountRule r) => x => x.Kind == r.Kind && x.TaxTypeId == r.TaxTypeId;

    private async Task<DiscountRuleDto> MapDiscountAsync(Guid id, CancellationToken ct) =>
        ToDto(await db.DiscountRules.Include(x => x.TaxType).SingleAsync(x => x.Id == id, ct));

    private static DiscountRuleDto ToDto(DiscountRule x) =>
        new(Header(x), TaxTypeRef(x.TaxType), x.Kind, x.Rate, x.CutoffMonth, x.CutoffDay, x.CutoffYearOffset);

    // --------------------------------------------------------- interest rules

    public async Task<Result<InterestRuleDto>> CreateInterestRuleAsync(CreateInterestRuleRequest request, CancellationToken cancellationToken = default)
    {
        var invalid = await ValidateAsync<InterestRuleDto, CreateInterestRuleRequest>(interestValidator, request, cancellationToken);
        if (invalid is not null) return invalid;
        var refs = await CheckTaxTypeAsync<InterestRuleDto>(request.TaxTypeId, cancellationToken);
        if (refs is not null) return refs;

        var rule = Stamp(new InterestRule
        {
            TaxTypeId = request.TaxTypeId, RatePerMonth = request.RatePerMonth, MaxMonths = request.MaxMonths, MonthCounting = request.MonthCounting,
        }, request);
        return await CreateAsync(db.InterestRules, rule, ScopeOf(rule), MapInterestAsync, cancellationToken);
    }

    public Task<Result<InterestRuleDto>> ApproveInterestRuleAsync(Guid id, CancellationToken cancellationToken = default) =>
        ApproveAsync(db.InterestRules, id, ScopeOf, MapInterestAsync, cancellationToken);

    public Task<Result<InterestRuleDto>> GetInterestRuleAsync(Guid id, CancellationToken cancellationToken = default) =>
        GetAsync(db.InterestRules, id, MapInterestAsync, cancellationToken);

    public async Task<Result<IReadOnlyList<InterestRuleDto>>> ListInterestRulesAsync(DateOnly? asOf, CancellationToken cancellationToken = default) =>
        Result.Success<IReadOnlyList<InterestRuleDto>>(
            (await Filter(db.InterestRules.Include(x => x.TaxType), asOf).ToListAsync(cancellationToken)).Select(ToDto).ToList());

    private static Expression<Func<InterestRule, bool>> ScopeOf(InterestRule r) => x => x.TaxTypeId == r.TaxTypeId;

    private async Task<InterestRuleDto> MapInterestAsync(Guid id, CancellationToken ct) =>
        ToDto(await db.InterestRules.Include(x => x.TaxType).SingleAsync(x => x.Id == id, ct));

    private static InterestRuleDto ToDto(InterestRule x) =>
        new(Header(x), TaxTypeRef(x.TaxType), x.RatePerMonth, x.MaxMonths, x.MonthCounting);

    // ---------------------------------------------------------- penalty rules

    public async Task<Result<PenaltyRuleDto>> CreatePenaltyRuleAsync(CreatePenaltyRuleRequest request, CancellationToken cancellationToken = default)
    {
        var invalid = await ValidateAsync<PenaltyRuleDto, CreatePenaltyRuleRequest>(penaltyValidator, request, cancellationToken);
        if (invalid is not null) return invalid;
        var refs = await CheckTaxTypeAsync<PenaltyRuleDto>(request.TaxTypeId, cancellationToken);
        if (refs is not null) return refs;

        var rule = Stamp(new PenaltyRule
        {
            TaxTypeId = request.TaxTypeId, Rate = request.Rate, FixedAmount = request.FixedAmount, AppliesAfterDays = request.AppliesAfterDays,
        }, request);
        return await CreateAsync(db.PenaltyRules, rule, ScopeOf(rule), MapPenaltyAsync, cancellationToken);
    }

    public Task<Result<PenaltyRuleDto>> ApprovePenaltyRuleAsync(Guid id, CancellationToken cancellationToken = default) =>
        ApproveAsync(db.PenaltyRules, id, ScopeOf, MapPenaltyAsync, cancellationToken);

    public Task<Result<PenaltyRuleDto>> GetPenaltyRuleAsync(Guid id, CancellationToken cancellationToken = default) =>
        GetAsync(db.PenaltyRules, id, MapPenaltyAsync, cancellationToken);

    public async Task<Result<IReadOnlyList<PenaltyRuleDto>>> ListPenaltyRulesAsync(DateOnly? asOf, CancellationToken cancellationToken = default) =>
        Result.Success<IReadOnlyList<PenaltyRuleDto>>(
            (await Filter(db.PenaltyRules.Include(x => x.TaxType), asOf).ToListAsync(cancellationToken)).Select(ToDto).ToList());

    private static Expression<Func<PenaltyRule, bool>> ScopeOf(PenaltyRule r) => x => x.TaxTypeId == r.TaxTypeId;

    private async Task<PenaltyRuleDto> MapPenaltyAsync(Guid id, CancellationToken ct) =>
        ToDto(await db.PenaltyRules.Include(x => x.TaxType).SingleAsync(x => x.Id == id, ct));

    private static PenaltyRuleDto ToDto(PenaltyRule x) =>
        new(Header(x), TaxTypeRef(x.TaxType), x.Rate, x.FixedAmount, x.AppliesAfterDays);

    // ----------------------------------------------------- tax increase caps

    public async Task<Result<TaxIncreaseCapRuleDto>> CreateTaxIncreaseCapRuleAsync(CreateTaxIncreaseCapRuleRequest request, CancellationToken cancellationToken = default)
    {
        var invalid = await ValidateAsync<TaxIncreaseCapRuleDto, CreateTaxIncreaseCapRuleRequest>(capValidator, request, cancellationToken);
        if (invalid is not null) return invalid;
        var refs = await CheckTaxTypeAsync<TaxIncreaseCapRuleDto>(request.TaxTypeId, cancellationToken);
        if (refs is not null) return refs;
        var smv = await db.Smvs.AsNoTracking().SingleOrDefaultAsync(s => s.Id == request.SmvId, cancellationToken);
        if (smv is null)
        {
            return Result.Failure<TaxIncreaseCapRuleDto>("SMV_NOT_FOUND", "The specified SMV does not exist.");
        }
        if (request.EffectiveDate < smv.EffectivityDate)
        {
            return Result.Failure<TaxIncreaseCapRuleDto>("VALIDATION_FAILED",
                $"A cap on increases from this SMV cannot start before the SMV takes effect ({smv.EffectivityDate:yyyy-MM-dd}).");
        }

        var rule = Stamp(new TaxIncreaseCapRule
        {
            SmvId = request.SmvId, TaxTypeId = request.TaxTypeId, Basis = request.Basis, Baseline = request.Baseline,
            MaxIncreasePercent = request.MaxIncreasePercent,
        }, request);
        rule.EndDate = request.EndDate;
        return await CreateAsync(db.TaxIncreaseCapRules, rule, ScopeOf(rule), MapCapAsync, cancellationToken);
    }

    public Task<Result<TaxIncreaseCapRuleDto>> ApproveTaxIncreaseCapRuleAsync(Guid id, CancellationToken cancellationToken = default) =>
        ApproveAsync(db.TaxIncreaseCapRules, id, ScopeOf, MapCapAsync, cancellationToken, RequireApprovedSmvAsync);

    public Task<Result<TaxIncreaseCapRuleDto>> GetTaxIncreaseCapRuleAsync(Guid id, CancellationToken cancellationToken = default) =>
        GetAsync(db.TaxIncreaseCapRules, id, MapCapAsync, cancellationToken);

    public async Task<Result<IReadOnlyList<TaxIncreaseCapRuleDto>>> ListTaxIncreaseCapRulesAsync(DateOnly? asOf, CancellationToken cancellationToken = default) =>
        Result.Success<IReadOnlyList<TaxIncreaseCapRuleDto>>(
            (await Filter(db.TaxIncreaseCapRules.Include(x => x.Smv).Include(x => x.TaxType), asOf).ToListAsync(cancellationToken)).Select(ToDto).ToList());

    private static Expression<Func<TaxIncreaseCapRule, bool>> ScopeOf(TaxIncreaseCapRule r) =>
        x => x.SmvId == r.SmvId && x.TaxTypeId == r.TaxTypeId && x.Basis == r.Basis;

    /// <summary>A cap may only go into force against an SMV that is itself in force.</summary>
    private async Task<Result<TaxIncreaseCapRuleDto>?> RequireApprovedSmvAsync(TaxIncreaseCapRule rule, CancellationToken ct) =>
        await db.Smvs.AnyAsync(s => s.Id == rule.SmvId && (s.Status == WorkflowStatus.Approved || s.Status == WorkflowStatus.Posted), ct)
            ? null
            : Result.Failure<TaxIncreaseCapRuleDto>("SMV_NOT_APPROVED", "The SMV this cap refers to must be approved before the cap can be approved.");

    private async Task<TaxIncreaseCapRuleDto> MapCapAsync(Guid id, CancellationToken ct) =>
        ToDto(await db.TaxIncreaseCapRules.Include(x => x.Smv).Include(x => x.TaxType).SingleAsync(x => x.Id == id, ct));

    private static TaxIncreaseCapRuleDto ToDto(TaxIncreaseCapRule x) =>
        new(Header(x), new SmvRefDto(x.Smv!.Id, x.Smv.OrdinanceNumber, x.Smv.EffectivityDate, x.Smv.RevisionYear),
            TaxTypeRef(x.TaxType), x.Basis, x.Baseline, x.MaxIncreasePercent);

    // ------------------------------------------------------- shared lifecycle

    private static async Task<Result<TDto>?> ValidateAsync<TDto, TRequest>(IValidator<TRequest> validator, TRequest request, CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        return validation.IsValid
            ? null
            : Result.Failure<TDto>("VALIDATION_FAILED", string.Join("; ", validation.Errors.Select(e => e.ErrorMessage).Distinct()));
    }

    private async Task<Result<TDto>?> CheckTaxTypeAsync<TDto>(Guid? taxTypeId, CancellationToken ct) =>
        taxTypeId is { } id && !await db.TaxTypes.AnyAsync(t => t.Id == id, ct)
            ? Result.Failure<TDto>("TAX_TYPE_NOT_FOUND", "The specified tax type does not exist.")
            : null;

    private static T Stamp<T>(T rule, IBillingRuleRequest request) where T : BillingRule
    {
        rule.LegalBasis = request.LegalBasis.Trim();
        rule.OrdinanceNumber = request.OrdinanceNumber.Trim();
        rule.OrdinanceDate = request.OrdinanceDate;
        rule.EffectiveDate = request.EffectiveDate;
        rule.Remarks = request.Remarks?.Trim();
        rule.Status = WorkflowStatus.Draft;
        return rule;
    }

    private async Task<Result<TDto>> CreateAsync<T, TDto>(
        DbSet<T> set, T rule, Expression<Func<T, bool>> sameScope, Func<Guid, CancellationToken, Task<TDto>> map, CancellationToken ct)
        where T : BillingRule
    {
        // Early feedback only — approval re-checks, since rules can be approved in between.
        var conflict = await EffectiveDateConflictAsync(set, sameScope, rule, excludeId: null, ct);
        if (conflict is not null)
        {
            return Result.Failure<TDto>("BILLING_RULE_EFFECTIVE_DATE_CONFLICT", conflict);
        }

        set.Add(rule);
        await db.SaveChangesAsync(ct);
        return Result.Success(await map(rule.Id, ct));
    }

    private async Task<Result<TDto>> ApproveAsync<T, TDto>(
        DbSet<T> set, Guid id, Func<T, Expression<Func<T, bool>>> scopeOf, Func<Guid, CancellationToken, Task<TDto>> map, CancellationToken ct,
        Func<T, CancellationToken, Task<Result<TDto>?>>? precheck = null)
        where T : BillingRule
    {
        var rule = await set.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (rule is null)
        {
            return Result.Failure<TDto>("BILLING_RULE_NOT_FOUND", "No billing rule of this type was found with the given id.");
        }
        if (rule.Status != WorkflowStatus.Draft)
        {
            return Result.Failure<TDto>("BILLING_RULE_NOT_DRAFT", $"Only a Draft rule can be approved; this rule is {rule.Status}.");
        }
        if (currentUser.AppUserId is not null && rule.CreatedBy == currentUser.AppUserId)
        {
            return Result.Failure<TDto>("CANNOT_APPROVE_OWN_BILLING_RULE", "The rule's creator cannot also approve it (maker-checker, CLAUDE.md §46).");
        }

        if (precheck is not null && await precheck(rule, ct) is { } failed)
        {
            return failed;
        }

        var sameScope = scopeOf(rule);
        var conflict = await EffectiveDateConflictAsync(set, sameScope, rule, rule.Id, ct);
        if (conflict is not null)
        {
            return Result.Failure<TDto>("BILLING_RULE_EFFECTIVE_DATE_CONFLICT", conflict);
        }

        var open = await set.Where(sameScope)
            .SingleOrDefaultAsync(x => x.Id != rule.Id && x.Status == WorkflowStatus.Approved && x.EndDate == null, ct);

        // Close the predecessor before approving its successor: the
        // one-open-approved-rule unique index is not deferrable.
        var ownsTransaction = db.Database.CurrentTransaction is null;
        var transaction = ownsTransaction ? await db.Database.BeginTransactionAsync(ct) : null;
        try
        {
            if (open is not null)
            {
                open.EndDate = rule.EffectiveDate.AddDays(-1);
                await db.SaveChangesAsync(ct);
            }

            rule.Status = WorkflowStatus.Approved;
            rule.ApprovedBy = currentUser.AppUserId;
            rule.ApprovedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);

            if (transaction is not null)
            {
                await transaction.CommitAsync(ct);
            }
        }
        catch (DbUpdateException)
        {
            // Unique index (open-ended rules) or exclusion constraint (bounded windows): a concurrent approval.
            return Result.Failure<TDto>("BILLING_RULE_APPROVAL_CONFLICT",
                "Another rule for the same scope was approved at the same time. Nothing was changed; reload and try again.");
        }
        finally
        {
            if (transaction is not null)
            {
                await transaction.DisposeAsync();
            }
        }

        return Result.Success(await map(rule.Id, ct));
    }

    /// <summary>
    /// A rule must start after every approved rule of its scope starts and —
    /// for rules approved with a bounded window, like tax increase caps —
    /// after every such window ends. Null = no conflict.
    /// </summary>
    private static async Task<string?> EffectiveDateConflictAsync<T>(
        DbSet<T> set, Expression<Func<T, bool>> sameScope, T rule, Guid? excludeId, CancellationToken ct)
        where T : BillingRule
    {
        var approved = set.Where(sameScope).Where(x => x.Status == WorkflowStatus.Approved && x.Id != excludeId);
        var latestStart = await approved.MaxAsync(x => (DateOnly?)x.EffectiveDate, ct);
        if (latestStart is { } start && rule.EffectiveDate <= start)
        {
            return $"An approved rule for the same scope is already effective from {start:yyyy-MM-dd}; a new rule must start after it.";
        }
        var latestEnd = await approved.MaxAsync(x => x.EndDate, ct);
        if (latestEnd is { } end && rule.EffectiveDate <= end)
        {
            return $"An approved rule for the same scope is in force until {end:yyyy-MM-dd}; a new rule must start after it.";
        }
        return null;
    }

    private static async Task<Result<TDto>> GetAsync<T, TDto>(DbSet<T> set, Guid id, Func<Guid, CancellationToken, Task<TDto>> map, CancellationToken ct)
        where T : BillingRule =>
        await set.AnyAsync(x => x.Id == id, ct)
            ? Result.Success(await map(id, ct))
            : Result.Failure<TDto>("BILLING_RULE_NOT_FOUND", "No billing rule of this type was found with the given id.");

    /// <summary>asOf given → approved rules in force on that date; otherwise every rule, newest first.</summary>
    private static IQueryable<T> Filter<T>(IQueryable<T> query, DateOnly? asOf) where T : BillingRule =>
        (asOf is { } date
            ? query.Where(x => x.Status == WorkflowStatus.Approved && x.EffectiveDate <= date && (x.EndDate == null || x.EndDate >= date))
            : query)
        .OrderByDescending(x => x.EffectiveDate).ThenByDescending(x => x.CreatedAt);

    private static BillingRuleHeaderDto Header(BillingRule x) => new(
        x.Id, x.LegalBasis, x.OrdinanceNumber, x.OrdinanceDate, x.EffectiveDate, x.EndDate,
        x.Status, x.CreatedBy, x.CreatedAt, x.ApprovedBy, x.ApprovedAt, x.Remarks);

    private static TaxTypeRefDto? TaxTypeRef(Domain.Entities.Reference.TaxType? t) => t is null ? null : new(t.Id, t.Code, t.Name);
}
