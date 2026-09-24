using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Prime.Application.Features.AssessmentLevels;
using Prime.Application.Features.Assessments;
using Prime.Application.Features.Billing.Bills;
using Prime.Application.Features.Smv;
using Prime.Application.Features.Valuation;
using Prime.Domain.Entities;
using Prime.Domain.Entities.Billing;
using Prime.Domain.Entities.Reference;
using Prime.Domain.Enums;
using Prime.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Prime.IntegrationTests;

/// <summary>
/// Phase 8 exit criteria (docs/DEVELOPMENT-ROADMAP.md): given a posted
/// assessment and DEMO billing rules, a bill is generated with an auditable
/// breakdown, posted, and shown on the statement of account. Same
/// rolled-back-transaction pattern as AssessmentFlowTests. Every rate and
/// amount is DEMO test data (CLAUDE.md §81).
///
/// The dev database may already hold approved billing rules, which would be
/// "in force" for these tests too, so each test first retires them inside
/// its own (rolled-back) transaction.
/// </summary>
public class BillingFlowTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    internal sealed record Seed(Guid PropertyId, Guid RpuId, Guid AssessmentId, Guid SmvId, Guid ClassificationId, TaxDeclaration TaxDeclaration);

    private static async Task<(PrimeDbContext Db, IServiceProvider Services, IAsyncDisposable Transaction)> BeginTestScopeAsync(WebApplicationFactory<Program> factory)
    {
        var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PrimeDbContext>();
        var transaction = await db.Database.BeginTransactionAsync();
        await RetireExistingRulesAsync(db);
        return (db, scope.ServiceProvider, new ScopedTransaction(transaction, scope));
    }

    private sealed class ScopedTransaction(IDbContextTransaction transaction, IServiceScope scope) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            await transaction.DisposeAsync();
            scope.Dispose();
        }
    }

    internal static async Task RetireExistingRulesAsync(PrimeDbContext db)
    {
        await Retire(db.TaxRates);
        await Retire(db.PaymentSchedules);
        await Retire(db.DiscountRules);
        await Retire(db.InterestRules);
        await Retire(db.PenaltyRules);
        await Retire(db.TaxIncreaseCapRules);

        static Task<int> Retire<T>(IQueryable<T> rules) where T : BillingRule =>
            rules.Where(r => r.Status == WorkflowStatus.Approved)
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.Status, WorkflowStatus.Cancelled).SetProperty(r => r.ApprovedAt, (DateTimeOffset?)null));
    }

    /// <summary>Land RPU with a POSTED assessment (AV 100,000 = 500 sqm × 1,000 × 20%) effective <paramref name="assessmentEffective"/>, and a taxable TD.</summary>
    internal static async Task<Seed> SeedPostedAssessmentAsync(IServiceProvider services, PrimeDbContext db,
        DateOnly assessmentEffective, Taxability taxability = Taxability.Taxable, bool post = true)
    {
        var province = new Province { PsgcCode = $"P{Guid.NewGuid():N}"[..10], Name = "Demo Province" };
        var municipality = new Municipality { Province = province, PsgcCode = $"M{Guid.NewGuid():N}"[..10], Name = "Demo Municipality" };
        var barangay = new Barangay { Municipality = municipality, PsgcCode = $"B{Guid.NewGuid():N}"[..10], Name = "Demo Barangay" };
        var classification = new Classification { Code = $"CL{Guid.NewGuid():N}"[..8], Name = "DEMO_Residential" };
        var actualUse = new ActualUse { Code = $"AU{Guid.NewGuid():N}"[..8], Name = "DEMO_Residential Use" };
        var propertyType = await TestSeed.LandPropertyTypeAsync(db);
        db.AddRange(province, municipality, barangay, classification, actualUse);

        var property = new PropertyEntity
        {
            PropertyIdentificationNumber = $"PIN-{Guid.NewGuid():N}",
            Province = province,
            Municipality = municipality,
            Barangay = barangay,
        };
        var rpu = new RealPropertyUnit { Property = property, RpuNumber = $"RPU-{Guid.NewGuid():N}", RpuType = RpuType.Land, EffectivityDate = new DateOnly(2024, 1, 1) };
        var land = new Land { Rpu = rpu, Property = property, Area = 500m, Classification = classification, ActualUse = actualUse };
        var taxDeclaration = new TaxDeclaration
        {
            Rpu = rpu, Property = property, TaxDeclarationNumber = $"TD-{Guid.NewGuid():N}", EffectivityDate = new DateOnly(2024, 1, 1),
            Taxability = taxability, Classification = classification, ActualUse = actualUse, AssessmentYear = 2024, Status = WorkflowStatus.Approved,
        };
        db.AddRange(property, rpu, land, taxDeclaration);
        await db.SaveChangesAsync();

        var smvService = services.GetRequiredService<ISmvService>();
        var smv = (await smvService.CreateSmvAsync(new CreateSmvRequest(
            $"ORD-{Guid.NewGuid():N}", new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 15), new DateOnly(2026, 1, 1), 2026, "DEMO_SMV for BillingFlowTests"))).Value;
        await smvService.ApproveSmvAsync(smv.Id);
        var schedule = (await smvService.CreateScheduleAsync(smv.Id, new CreateSmvScheduleRequest(
            classification.Id, actualUse.Id, propertyType.Id, null, "per sqm", 1000m, null, null, new DateOnly(2026, 1, 1)))).Value;
        await smvService.ApproveScheduleAsync(schedule.Id);

        var levels = services.GetRequiredService<IAssessmentLevelService>();
        var level = (await levels.CreateAsync(new CreateAssessmentLevelRequest(
            $"ORD-{Guid.NewGuid():N}", new DateOnly(2026, 1, 1), classification.Id, actualUse.Id, propertyType.Id,
            0m, 1_000_000m, 20m, new DateOnly(2026, 1, 1)))).Value;
        await levels.ApproveAsync(level.Id);

        var valuation = await services.GetRequiredService<IValuationService>().ComputeForLandAsync(land.Id);
        valuation.IsSuccess.ShouldBeTrue(valuation.IsSuccess ? null : valuation.Message);
        var created = await services.GetRequiredService<IAssessmentService>().CreateAsync(new CreateAssessmentRequest(
            valuation.Value.Id, 2026, new DateOnly(2026, 1, 1), null, null, "DEMO"));
        created.IsSuccess.ShouldBeTrue(created.IsSuccess ? null : created.Message);

        // The approve/post workflow is covered by AssessmentFlowTests; set the outcome directly.
        var assessment = await db.Assessments.SingleAsync(x => x.Id == created.Value.Id);
        assessment.Status = post ? WorkflowStatus.Posted : WorkflowStatus.Draft;
        assessment.EffectiveDate = assessmentEffective;
        await db.SaveChangesAsync();

        return new Seed(property.Id, rpu.Id, assessment.Id, smv.Id, classification.Id, taxDeclaration);
    }

    internal static T Approved<T>(T rule, DateOnly effective, DateOnly? end = null) where T : BillingRule
    {
        rule.LegalBasis = "DEMO — not an ordinance";
        rule.OrdinanceNumber = "DEMO-ORD";
        rule.EffectiveDate = effective;
        rule.EndDate = end;
        rule.Status = WorkflowStatus.Approved;
        rule.ApprovedAt = DateTimeOffset.UtcNow;
        return rule;
    }

    internal static async Task<(TaxType Basic, TaxType Sef)> SeedRulesAsync(PrimeDbContext db, DateOnly effective, decimal? promptDiscount = 10m)
    {
        var basic = new TaxType { Code = $"DB{Guid.NewGuid():N}"[..8], Name = "DEMO_BASIC" };
        var sef = new TaxType { Code = $"DS{Guid.NewGuid():N}"[..8], Name = "DEMO_SEF" };
        db.AddRange(basic, sef);
        db.AddRange(
            Approved(new TaxRate { TaxType = basic, Rate = 1m }, effective),
            Approved(new TaxRate { TaxType = sef, Rate = 1m }, effective),
            Approved(new PaymentSchedule { Installments = [new() { Sequence = 1, DueMonth = 3, DueDay = 31, SharePercent = 100m }] }, effective),
            Approved(new InterestRule { RatePerMonth = 2m, MaxMonths = 36, MonthCounting = InterestMonthCounting.FractionCountsAsFullMonth }, effective));
        if (promptDiscount is { } rate)
        {
            db.Add(Approved(new DiscountRule { Kind = DiscountKind.PromptPayment, Rate = rate }, effective));
        }
        await db.SaveChangesAsync();
        return (basic, sef);
    }

    [Fact]
    public async Task Generate_FromPostedAssessment_BuildsAuditableBill()
    {
        var (db, services, transaction) = await BeginTestScopeAsync(factory);
        await using var _ = transaction;
        var seed = await SeedPostedAssessmentAsync(services, db, new DateOnly(2026, 1, 1));
        await SeedRulesAsync(db, new DateOnly(2026, 1, 1));
        var bills = services.GetRequiredService<IBillService>();

        var result = await bills.GenerateAsync(new GenerateBillRequest(seed.RpuId, 2026, new DateOnly(2026, 1, 15)));

        result.IsSuccess.ShouldBeTrue(result.IsSuccess ? null : result.Message);
        var bill = result.Value;
        bill.Status.ShouldBe(WorkflowStatus.Draft);
        bill.AssessmentId.ShouldBe(seed.AssessmentId);
        bill.AssessedValue.ShouldBe(100_000m);
        bill.RulesAsOfDate.ShouldBe(new DateOnly(2026, 1, 1));
        bill.TaxTypes.Select(t => t.AnnualTax).ShouldBe([1_000m, 1_000m], ignoreOrder: true);
        bill.Details.Count(d => d.Component == BillingComponent.Tax).ShouldBe(2);
        bill.Details.Where(d => d.Component == BillingComponent.Discount).Sum(d => d.Amount).ShouldBe(-200m);
        bill.Total.ShouldBe(1_800m);
        bill.Details.ShouldAllBe(d => d.RuleId != Guid.Empty && d.Explanation != "");
    }

    [Fact]
    public async Task Generate_Overdue_ChargesInterestNotDiscount()
    {
        var (db, services, transaction) = await BeginTestScopeAsync(factory);
        await using var _ = transaction;
        var seed = await SeedPostedAssessmentAsync(services, db, new DateOnly(2026, 1, 1));
        await SeedRulesAsync(db, new DateOnly(2026, 1, 1));

        var bill = (await services.GetRequiredService<IBillService>().GenerateAsync(
            new GenerateBillRequest(seed.RpuId, 2026, new DateOnly(2026, 6, 15)))).Value;

        bill.Details.ShouldNotContain(d => d.Component == BillingComponent.Discount);
        bill.Details.Where(d => d.Component == BillingComponent.Interest).Sum(d => d.Amount).ShouldBe(120m); // 2,000 × 2% × 3 months
        bill.Total.ShouldBe(2_120m);
    }

    [Fact]
    public async Task Generate_SameRpuYearAndAsOf_IsDuplicate_UntilCancelled()
    {
        var (db, services, transaction) = await BeginTestScopeAsync(factory);
        await using var _ = transaction;
        var seed = await SeedPostedAssessmentAsync(services, db, new DateOnly(2026, 1, 1));
        await SeedRulesAsync(db, new DateOnly(2026, 1, 1));
        var bills = services.GetRequiredService<IBillService>();
        var request = new GenerateBillRequest(seed.RpuId, 2026, new DateOnly(2026, 1, 15));

        var first = (await bills.GenerateAsync(request)).Value;
        var again = await bills.GenerateAsync(request);
        again.Code.ShouldBe("BILL_DUPLICATE");

        (await bills.CancelAsync(first.Id, "DEMO correction")).Value.Status.ShouldBe(WorkflowStatus.Cancelled);
        (await bills.GenerateAsync(request)).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task Post_SupersedesPreviousPostedBill_AndStatementShowsOnlyTheLatest()
    {
        var (db, services, transaction) = await BeginTestScopeAsync(factory);
        await using var _ = transaction;
        var seed = await SeedPostedAssessmentAsync(services, db, new DateOnly(2026, 1, 1));
        await SeedRulesAsync(db, new DateOnly(2026, 1, 1));
        var bills = services.GetRequiredService<IBillService>();

        var january = (await bills.GenerateAsync(new GenerateBillRequest(seed.RpuId, 2026, new DateOnly(2026, 1, 15)))).Value;
        (await bills.PostAsync(january.Id)).Value.Status.ShouldBe(WorkflowStatus.Posted);
        var june = (await bills.GenerateAsync(new GenerateBillRequest(seed.RpuId, 2026, new DateOnly(2026, 6, 15)))).Value;
        var posted = await bills.PostAsync(june.Id);

        posted.IsSuccess.ShouldBeTrue(posted.IsSuccess ? null : posted.Message);
        var superseded = (await bills.GetByIdAsync(january.Id)).Value;
        superseded.Status.ShouldBe(WorkflowStatus.Cancelled);
        superseded.SupersededByBillId.ShouldBe(june.Id);

        var statement = (await bills.GetStatementOfAccountAsync(seed.PropertyId)).Value;
        statement.Bills.Single().BillId.ShouldBe(june.Id);
        statement.Bills.Single().Interest.ShouldBe(120m);
        statement.TotalBilled.ShouldBe(2_120m);
    }

    [Fact]
    public async Task Generate_CapUsesPreviousPostedBillAsBaseline()
    {
        var (db, services, transaction) = await BeginTestScopeAsync(factory);
        await using var _ = transaction;
        var seed = await SeedPostedAssessmentAsync(services, db, new DateOnly(2025, 1, 1));
        var basic = new TaxType { Code = $"DB{Guid.NewGuid():N}"[..8], Name = "DEMO_BASIC" };
        db.AddRange(basic,
            Approved(new TaxRate { TaxType = basic, Rate = 0.5m }, new DateOnly(2025, 1, 1), new DateOnly(2025, 12, 31)),
            Approved(new TaxRate { TaxType = basic, Rate = 1m }, new DateOnly(2026, 1, 1)),
            Approved(new PaymentSchedule { Installments = [new() { Sequence = 1, DueMonth = 3, DueDay = 31, SharePercent = 100m }] }, new DateOnly(2025, 1, 1)),
            Approved(new TaxIncreaseCapRule
            {
                SmvId = seed.SmvId, Basis = TaxIncreaseCapBasis.StatutoryFirstYear, Baseline = TaxIncreaseCapBaseline.TaxBeforeSmv,
                MaxIncreasePercent = 6m, // DEMO
            }, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31)));
        await db.SaveChangesAsync();
        var bills = services.GetRequiredService<IBillService>();

        // Without a pre-SMV bill there is no baseline: the cap is noted, not applied.
        var uncapped = (await bills.GenerateAsync(new GenerateBillRequest(seed.RpuId, 2026, new DateOnly(2026, 1, 10)))).Value;
        uncapped.Total.ShouldBe(1_000m);
        uncapped.Notes.ShouldNotBeNull().ShouldContain("not applied");

        var bill2025 = (await bills.GenerateAsync(new GenerateBillRequest(seed.RpuId, 2025, new DateOnly(2025, 1, 10)))).Value;
        bill2025.Total.ShouldBe(500m);
        (await bills.PostAsync(bill2025.Id)).IsSuccess.ShouldBeTrue();

        var capped = (await bills.GenerateAsync(new GenerateBillRequest(seed.RpuId, 2026, new DateOnly(2026, 1, 15)))).Value;
        var basicTax = capped.TaxTypes.Single();
        basicTax.ComputedAnnualTax.ShouldBe(1_000m);
        basicTax.CapBaselineTax.ShouldBe(500m);
        basicTax.CapLimit.ShouldBe(530m);
        basicTax.AnnualTax.ShouldBe(530m);
        capped.Total.ShouldBe(530m);
    }

    [Fact]
    public async Task Generate_WithoutPostedAssessmentInForce_IsRefused()
    {
        var (db, services, transaction) = await BeginTestScopeAsync(factory);
        await using var _ = transaction;
        var seed = await SeedPostedAssessmentAsync(services, db, new DateOnly(2026, 1, 1));
        await SeedRulesAsync(db, new DateOnly(2025, 1, 1));

        var result = await services.GetRequiredService<IBillService>().GenerateAsync(
            new GenerateBillRequest(seed.RpuId, 2025, new DateOnly(2025, 1, 15)));

        result.Code.ShouldBe("BILL_ASSESSMENT_NOT_FOUND");
    }

    [Fact]
    public async Task Generate_ExemptTaxDeclaration_IsRefused()
    {
        var (db, services, transaction) = await BeginTestScopeAsync(factory);
        await using var _ = transaction;
        var seed = await SeedPostedAssessmentAsync(services, db, new DateOnly(2026, 1, 1), Taxability.Exempt);
        await SeedRulesAsync(db, new DateOnly(2026, 1, 1));

        var result = await services.GetRequiredService<IBillService>().GenerateAsync(
            new GenerateBillRequest(seed.RpuId, 2026, new DateOnly(2026, 1, 15)));

        result.Code.ShouldBe("BILL_PROPERTY_EXEMPT");
    }

    [Fact]
    public async Task Generate_NoPaymentScheduleInForce_IsRefused()
    {
        var (db, services, transaction) = await BeginTestScopeAsync(factory);
        await using var _ = transaction;
        var seed = await SeedPostedAssessmentAsync(services, db, new DateOnly(2026, 1, 1));
        var basic = new TaxType { Code = $"DB{Guid.NewGuid():N}"[..8], Name = "DEMO_BASIC" };
        db.AddRange(basic, Approved(new TaxRate { TaxType = basic, Rate = 1m }, new DateOnly(2026, 1, 1)));
        await db.SaveChangesAsync();

        var result = await services.GetRequiredService<IBillService>().GenerateAsync(
            new GenerateBillRequest(seed.RpuId, 2026, new DateOnly(2026, 1, 15)));

        result.Code.ShouldBe("BILL_PAYMENT_SCHEDULE_NOT_FOUND");
    }
}
