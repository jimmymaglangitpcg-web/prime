using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Prime.Application.Features.Assessments;
using Prime.Application.Features.TaxDeclarations;
using Prime.Application.Features.Valuation;
using Prime.Domain.Entities.Transactions;
using Prime.Domain.Enums;
using Prime.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Prime.IntegrationTests;

/// <summary>
/// Step L1-2 (docs/analysis/valuation-foundation.md §4.2): the effectivity is derived from the
/// transaction type's rule and the date the assessment is made (final approval), never typed;
/// an override needs a reason; a December draft approved in January is stopped; a late
/// reassessment is flagged. DEMO transaction types, rolled back. The LGU clock is moved by hand.
/// </summary>
public class EffectivityRuleTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private sealed class MovableTime : TimeProvider
    {
        public DateTimeOffset Now { get; set; }
        public override DateTimeOffset GetUtcNow() => Now;
    }

    /// <summary>Noon in Manila (UTC+8) on <paramref name="date"/>.</summary>
    private static DateTimeOffset Noon(DateOnly date) => new(date.ToDateTime(new TimeOnly(4, 0)), TimeSpan.Zero);

    private sealed record Context(IServiceProvider Services, PrimeDbContext Db, BillingFlowTests.Seed Seed, MovableTime Time,
        TransactionType NextJanuary, TransactionType NextQuarter, IAsyncDisposable Scope);

    private async Task<Context> BeginAsync(DateOnly today)
    {
        var time = new MovableTime { Now = Noon(today) };
        var host = factory.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddSingleton<TimeProvider>(time)));
        var scope = host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PrimeDbContext>();
        var transaction = await db.Database.BeginTransactionAsync();
        // 500 sqm at 1,000/sqm (DEMO SMV from 2026, open-ended), level 20 %, a posted 2026 assessment.
        var seed = await BillingFlowTests.SeedPostedAssessmentAsync(scope.ServiceProvider, db, new DateOnly(2026, 1, 1));
        var nextJanuary = Approved(new TransactionType
        {
            Code = $"NJ{Guid.NewGuid():N}"[..10], Name = "DEMO new assessment", Kind = PropertyTransactionKind.NewAssessment,
            EffectivityRule = EffectivityRule.NextJanuary, EffectivityLegalBasis = "DEMO — LGC §221",
        });
        var nextQuarter = Approved(new TransactionType
        {
            Code = $"NQ{Guid.NewGuid():N}"[..10], Name = "DEMO reassessment", Kind = PropertyTransactionKind.Reassessment,
            EffectivityRule = EffectivityRule.NextQuarter, EffectivityLegalBasis = "DEMO — LAM Bk III p.85", CauseWindowDays = 90,
        });
        db.TransactionTypes.AddRange(nextJanuary, nextQuarter);
        await db.SaveChangesAsync();
        return new Context(scope.ServiceProvider, db, seed, time, nextJanuary, nextQuarter, new Disposer(transaction, scope, host));
    }

    private static TransactionType Approved(TransactionType type)
    {
        type.LegalBasis = "DEMO catalogue";
        type.EffectiveDate = new DateOnly(2020, 1, 1);
        type.Status = WorkflowStatus.Approved;
        type.ApprovedAt = DateTimeOffset.UtcNow;
        return type;
    }

    private sealed class Disposer(IAsyncDisposable transaction, IAsyncDisposable scope, IDisposable host) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            await transaction.DisposeAsync();
            await scope.DisposeAsync();
            host.Dispose();
        }
    }

    private static async Task<Guid> ValueAsync(Context c, DateOnly asOf)
    {
        var valuation = await c.Services.GetRequiredService<IValuationService>().ComputeForRpuAsync(c.Seed.RpuId, asOf: asOf);
        valuation.IsSuccess.ShouldBeTrue(valuation.Message);
        return valuation.Value.Id;
    }

    [Fact]
    public async Task A_new_assessment_takes_effect_next_January_and_a_December_draft_approved_in_January_is_stopped()
    {
        var c = await BeginAsync(new DateOnly(2026, 12, 31));
        await using var _ = c.Scope;
        var assessments = c.Services.GetRequiredService<IAssessmentService>();

        var effectivity = (await assessments.EffectivityAsync(c.NextJanuary.Id, null, null, null)).Value;
        (effectivity.EffectiveDate, effectivity.Year, effectivity.Quarter, effectivity.Derived, effectivity.TransactionCode)
            .ShouldBe((new DateOnly(2027, 1, 1), 2027, 1, true, c.NextJanuary.Code));

        // No effective date given: the rule supplies it; year and quarter are stored by the database.
        var request = new CreateAssessmentRequest(await ValueAsync(c, new DateOnly(2027, 1, 1)), null, null, c.Seed.AssessmentId, null, "DEMO",
            TransactionTypeId: c.NextJanuary.Id);
        var created = await assessments.CreateAsync(request);
        created.IsSuccess.ShouldBeTrue(created.Message);
        var draft = created.Value;
        (draft.EffectiveDate, draft.AssessmentYear, draft.EffectivityYear, draft.EffectivityQuarter, draft.TransactionCode, draft.EffectivityRule, draft.MadeOn)
            .ShouldBe((new DateOnly(2027, 1, 1), 2027, 2027, 1, c.NextJanuary.Code, (EffectivityRule?)EffectivityRule.NextJanuary, (DateOnly?)null));
        (await assessments.SubmitForReviewAsync(draft.Id)).IsSuccess.ShouldBeTrue();

        // Approved on 2 January, it would take effect in 2028: stopped, nothing signed.
        c.Time.Now = Noon(new DateOnly(2027, 1, 2));
        var late = await TestSeed.AsCheckerAsync(c.Services, () => assessments.ApproveAsync(draft.Id));
        late.Code.ShouldBe("EFFECTIVITY_CHANGED");
        late.Message!.ShouldContain("2028-01-01");
        (await c.Db.Assessments.AsNoTracking().SingleAsync(x => x.Id == draft.Id)).Status.ShouldBe(WorkflowStatus.PendingReview);

        // Approved on 1 January, it is made that day and takes effect that day.
        c.Time.Now = Noon(new DateOnly(2027, 1, 1));
        var approved = await TestSeed.AsCheckerAsync(c.Services, () => assessments.ApproveAsync(draft.Id));
        approved.IsSuccess.ShouldBeTrue(approved.Message);
        (approved.Value.Status, approved.Value.MadeOn).ShouldBe((WorkflowStatus.Approved, (DateOnly?)new DateOnly(2027, 1, 1)));
    }

    [Fact]
    public async Task A_derived_effectivity_is_overridden_only_with_a_reason()
    {
        var c = await BeginAsync(new DateOnly(2026, 6, 15));
        await using var _ = c.Scope;
        var assessments = c.Services.GetRequiredService<IAssessmentService>();
        var valuationId = await ValueAsync(c, new DateOnly(2026, 7, 1));

        var noReason = await assessments.CreateAsync(new CreateAssessmentRequest(valuationId, null, new DateOnly(2026, 7, 1), null, null, "DEMO",
            TransactionTypeId: c.NextJanuary.Id));
        noReason.Code.ShouldBe("EFFECTIVITY_OVERRIDE_REASON_REQUIRED");
        noReason.Message!.ShouldContain("2027-01-01");

        var overridden = await assessments.CreateAsync(new CreateAssessmentRequest(valuationId, null, new DateOnly(2026, 7, 1), null, null, "DEMO",
            TransactionTypeId: c.NextJanuary.Id, EffectivityOverrideReason: "DEMO court order"));
        overridden.IsSuccess.ShouldBeTrue(overridden.Message);
        (overridden.Value.EffectiveDate, overridden.Value.EffectivityQuarter, overridden.Value.EffectivityOverrideReason)
            .ShouldBe((new DateOnly(2026, 7, 1), 3, "DEMO court order"));

        // An override is not re-derived at approval.
        (await assessments.SubmitForReviewAsync(overridden.Value.Id)).IsSuccess.ShouldBeTrue();
        c.Time.Now = Noon(new DateOnly(2026, 8, 1));
        (await TestSeed.AsCheckerAsync(c.Services, () => assessments.ApproveAsync(overridden.Value.Id))).IsSuccess.ShouldBeTrue();

        // Without a type and without a date there is nothing to go on.
        (await assessments.CreateAsync(new CreateAssessmentRequest(valuationId, null, null, null, null, "DEMO"))).Code.ShouldBe("EFFECTIVE_DATE_REQUIRED");
        // The valuation date still has to match the effectivity (L1-1).
        (await assessments.CreateAsync(new CreateAssessmentRequest(valuationId, null, null, null, null, "DEMO", TransactionTypeId: c.NextJanuary.Id)))
            .Code.ShouldBe("VALUATION_DATE_MISMATCH");
    }

    [Fact]
    public async Task A_reassessment_takes_effect_next_quarter_and_is_flagged_when_late()
    {
        var c = await BeginAsync(new DateOnly(2026, 5, 20));
        await using var _ = c.Scope;
        var assessments = c.Services.GetRequiredService<IAssessmentService>();

        (await assessments.EffectivityAsync(c.NextQuarter.Id, null, null, null)).Code.ShouldBe("CAUSE_DATE_REQUIRED");
        (await assessments.EffectivityAsync(c.NextQuarter.Id, new DateOnly(2026, 5, 21), null, null)).Code.ShouldBe("CAUSE_DATE_IN_FUTURE");

        // Cause on 19 February: 90 days end on 20 May — in time.
        var inTime = (await assessments.EffectivityAsync(c.NextQuarter.Id, new DateOnly(2026, 2, 19), null, null)).Value;
        (inTime.EffectiveDate, inTime.Quarter, inTime.CauseWindowDays, inTime.CauseWindowExceeded).ShouldBe((new DateOnly(2026, 7, 1), 3, (int?)90, false));

        // Cause on 18 February: day 91 — flagged, not refused.
        var created = await assessments.CreateAsync(new CreateAssessmentRequest(await ValueAsync(c, new DateOnly(2026, 7, 1)), null, null,
            c.Seed.AssessmentId, null, "DEMO", TransactionTypeId: c.NextQuarter.Id, CauseDate: new DateOnly(2026, 2, 18)));
        created.IsSuccess.ShouldBeTrue(created.Message);
        (created.Value.EffectiveDate, created.Value.CauseDate, created.Value.CauseWindowExceeded)
            .ShouldBe((new DateOnly(2026, 7, 1), (DateOnly?)new DateOnly(2026, 2, 18), true));
    }

    [Fact]
    public async Task A_transaction_type_rule_names_its_legal_basis_and_only_NextQuarter_has_a_window()
    {
        var c = await BeginAsync(new DateOnly(2026, 1, 1));
        await using var _ = c.Scope;
        var transactions = c.Services.GetRequiredService<Prime.Application.Features.Transactions.ITransactionService>();
        Prime.Application.Features.Transactions.CreateTransactionTypeRequest Type(EffectivityRule? rule, string? basis, int? window) =>
            new("DEMO", new DateOnly(2099, 1, 1), null, $"V{Guid.NewGuid():N}"[..10], "DEMO", PropertyTransactionKind.Reassessment, null, null, [],
                rule, basis, window);

        (await transactions.CreateTypeAsync(Type(EffectivityRule.NextJanuary, null, null))).Message!.ShouldContain("effectivityLegalBasis is required");
        (await transactions.CreateTypeAsync(Type(EffectivityRule.NextJanuary, "DEMO", 90))).Message!.ShouldContain("NextQuarter rule only");
        (await transactions.CreateTypeAsync(Type(null, "DEMO", null))).Message!.ShouldContain("only with an effectivity rule");
        var ok = await transactions.CreateTypeAsync(Type(EffectivityRule.NextQuarter, "DEMO", 90));
        ok.IsSuccess.ShouldBeTrue(ok.Message);
        (ok.Value.EffectivityRule, ok.Value.CauseWindowDays).ShouldBe(((EffectivityRule?)EffectivityRule.NextQuarter, (int?)90));
    }

    [Fact]
    public async Task The_TD_prepared_on_posting_carries_the_assessment_transaction_code()
    {
        var c = await BeginAsync(new DateOnly(2026, 1, 1));
        await using var _ = c.Scope;
        var assessments = c.Services.GetRequiredService<IAssessmentService>();
        var created = (await assessments.CreateAsync(new CreateAssessmentRequest(await ValueAsync(c, new DateOnly(2026, 1, 1)), null, null,
            null, null, "DEMO", TransactionTypeId: c.NextJanuary.Id))).Value;

        var stored = await c.Db.Assessments.AsNoTracking().SingleAsync(x => x.Id == created.Id);
        var code = await TransactionCodes.FromAssessmentAsync(c.Db, new FaasOptions(), stored, new DateOnly(2026, 1, 1), CancellationToken.None);
        code.Code.ShouldBe(c.NextJanuary.Code);
    }
}
