using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Prime.Application.Features.AssessmentLevels;
using Prime.Application.Features.Assessments;
using Prime.Application.Features.Billing.Bills;
using Prime.Application.Features.Collection;
using Prime.Application.Features.Forms;
using Prime.Application.Features.Lands;
using Prime.Application.Features.Parcels;
using Prime.Application.Features.Properties;
using Prime.Application.Features.RealPropertyUnits;
using Prime.Application.Features.Smv;
using Prime.Application.Features.TaxDeclarations;
using Prime.Application.Features.Taxpayers;
using Prime.Application.Features.Valuation;
using Prime.Domain.Common;
using Prime.Domain.Entities.Collection;
using Prime.Domain.Entities.Forms;
using Prime.Domain.Entities.Reference;
using Prime.Domain.Enums;
using Prime.Infrastructure.Identity;
using Prime.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Prime.IntegrationTests;

/// <summary>
/// CLAUDE.md §74 end-to-end flow and the Phase 9 exit criterion
/// (docs/DEVELOPMENT-ROADMAP.md): CREATE PROPERTY → CREATE TAXPAYER → CREATE
/// PARCEL → CREATE RPU → CREATE TAX DECLARATION → VALUATE → ASSESS → BILL →
/// PAY → VERIFY BALANCE, through the application services the API calls, with
/// their real workflows (maker-checker on SMV, assessment level, assessment,
/// TD and remittance). Runs in one rolled-back transaction with the LGU clock
/// pinned to 15 March 2026; every rule, rate, code and number format is DEMO
/// test data (CLAUDE.md §81), and the dev database's own approved
/// configuration is retired inside the transaction so only these apply.
///
/// DEMO arithmetic: 500 sqm × 1,000/sqm = MV 500,000; 20% level → AV 100,000;
/// basic 1% + SEF 1% = 2,000 tax, one installment due 31 March.
/// </summary>
public class EndToEndFlowTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private sealed class FixedTime(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private static async Task RetireAsync<T>(IQueryable<T> rows) where T : EffectiveDatedConfiguration =>
        await rows.Where(x => x.Status == WorkflowStatus.Approved)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, WorkflowStatus.Cancelled).SetProperty(x => x.ApprovedAt, (DateTimeOffset?)null));

    private static T Approved<T>(T item) where T : EffectiveDatedConfiguration
    {
        item.LegalBasis = "DEMO — not an LGU source";
        item.EffectiveDate = new DateOnly(2020, 1, 1);
        item.Status = WorkflowStatus.Approved;
        item.ApprovedAt = DateTimeOffset.UtcNow;
        return item;
    }

    private static T Ok<T>(Prime.Application.Common.Result<T> result, string step)
    {
        result.IsSuccess.ShouldBeTrue($"{step}: {result.Code} {result.Message}");
        return result.Value;
    }

    [Fact]
    public async Task CreatePropertyToVerifiedBalance()
    {
        var host = factory.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
            s.AddSingleton<TimeProvider>(new FixedTime(new DateTimeOffset(2026, 3, 15, 2, 0, 0, TimeSpan.Zero)))));
        using var scope = host.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<PrimeDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync();
        await BillingFlowTests.RetireExistingRulesAsync(db);
        await RetireAsync(db.ApprovalChains);
        await RetireAsync(db.NumberingSchemes);
        await RetireAsync(db.RevenueAccountMappings);

        var user = sp.GetRequiredService<CurrentUserService>();
        var maker = Guid.NewGuid();
        var checker = Guid.NewGuid();
        void As(Guid who) => user.AppUserId = who;
        var tag = Guid.NewGuid().ToString("N")[..8];

        // DEMO reference data.
        var province = new Province { PsgcCode = $"E2EP{tag}", Name = "DEMO_E2E Province" };
        var municipality = new Municipality { Province = province, PsgcCode = $"E2EM{tag}", Name = "DEMO_E2E Municipality" };
        var barangay = new Barangay { Municipality = municipality, PsgcCode = $"E2EB{tag}", Name = "DEMO_E2E Barangay" };
        var classification = new Classification { Code = $"E2ECL{tag}", Name = "DEMO_E2E Residential" };
        var actualUse = new ActualUse { Code = $"E2EAU{tag}", Name = "DEMO_E2E Residential use" };
        var ownershipType = new OwnershipType { Code = $"E2EOT{tag}", Name = "DEMO_E2E Sole" };
        var propertyType = await TestSeed.LandPropertyTypeAsync(db);
        db.AddRange(province, municipality, barangay, classification, actualUse, ownershipType);
        await db.SaveChangesAsync();

        As(maker);

        // 1. CREATE PROPERTY
        var property = Ok(await sp.GetRequiredService<IPropertyService>().CreateAsync(new CreatePropertyRequest(
            $"E2E-PIN-{tag}", province.Id, municipality.Id, barangay.Id, null, "DEMO Street", null, "L-1", "B-1", null, null, null)), "create property");

        // 2. CREATE TAXPAYER, and register ownership
        var taxpayers = sp.GetRequiredService<ITaxpayerService>();
        var taxpayer = Ok(await taxpayers.CreateAsync(new CreateTaxpayerRequest(
            TaxpayerType.Individual, "DEMO-DelaCruz", "Juan", null, null, null, $"E2E-TIN-{tag}", "DEMO Address",
            barangay.Id, municipality.Id, province.Id, null, null)), "create taxpayer");
        Ok(await taxpayers.AddOwnerAsync(new AddPropertyOwnerRequest(property.Id, taxpayer.Id, ownershipType.Id, 100m, new DateOnly(2026, 1, 1))), "add owner");

        // 3. CREATE PARCEL
        Ok(await sp.GetRequiredService<IParcelService>().CreateAsync(new CreateParcelRequest(
            property.Id, barangay.Id, null, "POLYGON((121.0 14.5, 121.0002 14.5, 121.0002 14.5002, 121.0 14.5002, 121.0 14.5))",
            500m, null, "L-1", "B-1")), "create parcel");

        // 4. CREATE RPU, with its land
        var rpu = Ok(await sp.GetRequiredService<IRealPropertyUnitService>().CreateAsync(new CreateRpuRequest(
            property.Id, $"E2E-RPU-{tag}", RpuType.Land, new DateOnly(2026, 1, 1), null)), "create RPU");
        Ok(await sp.GetRequiredService<ILandService>().CreateAsync(new CreateLandRequest(
            rpu.Id, 500m, null, classification.Id, actualUse.Id, null, null, null, null, null, false, null)), "create land");

        // Valuation rules (maker creates, checker approves).
        var smvService = sp.GetRequiredService<ISmvService>();
        var smv = Ok(await smvService.CreateSmvAsync(new CreateSmvRequest(
            $"E2E-ORD-{tag}", new DateOnly(2025, 12, 1), new DateOnly(2025, 12, 15), new DateOnly(2026, 1, 1), 2026, "DEMO_E2E SMV")), "create SMV");
        var schedule = Ok(await smvService.CreateScheduleAsync(smv.Id, new CreateSmvScheduleRequest(
            classification.Id, actualUse.Id, propertyType.Id, null, "per sqm", 1_000m, null, null, new DateOnly(2026, 1, 1))), "create SMV schedule");
        var levels = sp.GetRequiredService<IAssessmentLevelService>();
        var level = Ok(await levels.CreateAsync(new CreateAssessmentLevelRequest(
            $"E2E-ORD-{tag}", new DateOnly(2025, 12, 1), classification.Id, actualUse.Id, propertyType.Id, 0m, 10_000_000m, 20m,
            new DateOnly(2026, 1, 1))), "create assessment level");
        (await smvService.ApproveSmvAsync(smv.Id)).Code.ShouldBe("CANNOT_APPROVE_OWN_SMV");
        // An approval with no acting user is refused, never taken for someone else's (workflow-security.md G7).
        user.AppUserId = null;
        (await smvService.ApproveSmvAsync(smv.Id)).Code.ShouldBe("APPROVING_USER_UNKNOWN");
        (await levels.ApproveAsync(level.Id)).Code.ShouldBe("APPROVING_USER_UNKNOWN");
        As(checker);
        Ok(await smvService.ApproveSmvAsync(smv.Id), "approve SMV");
        Ok(await smvService.ApproveScheduleAsync(schedule.Id), "approve SMV schedule");
        Ok(await levels.ApproveAsync(level.Id), "approve assessment level");
        As(maker);

        // 5. VALUATE
        var valuation = Ok(await sp.GetRequiredService<IValuationService>().ComputeForRpuAsync(rpu.Id, asOf: new DateOnly(2026, 1, 1)), "valuate");
        valuation.ComputedMarketValue.ShouldBe(500_000m);

        // 6. ASSESS: draft → submitted → approved (by another user) → posted
        var assessments = sp.GetRequiredService<IAssessmentService>();
        var assessment = Ok(await assessments.CreateAsync(new CreateAssessmentRequest(valuation.Id, 2026, new DateOnly(2026, 1, 1), null, null, "DEMO E2E")), "assess");
        assessment.AssessedValue.ShouldBe(100_000m);
        Ok(await assessments.SubmitForReviewAsync(assessment.Id), "submit assessment");
        (await assessments.ApproveAsync(assessment.Id)).Code.ShouldBe("CANNOT_APPROVE_OWN_ASSESSMENT");
        user.AppUserId = null;
        (await assessments.ApproveAsync(assessment.Id)).Code.ShouldBe("APPROVING_USER_UNKNOWN");
        As(checker);
        Ok(await assessments.ApproveAsync(assessment.Id), "approve assessment");
        Ok(await assessments.PostAsync(assessment.Id), "post assessment");
        As(maker);

        // 7. CREATE TAX DECLARATION for the posted assessment, approved by another user
        var tds = sp.GetRequiredService<ITaxDeclarationService>();
        var td = Ok(await tds.CreateAsync(new CreateTaxDeclarationRequest(
            rpu.Id, $"E2E-TD-{tag}", new DateOnly(2026, 1, 1), Taxability.Taxable, classification.Id, actualUse.Id, null, 2026, null, "DEMO E2E",
            AssessmentId: assessment.Id)), "create TD");
        Ok(await tds.SubmitForReviewAsync(td.Id), "submit TD");
        user.AppUserId = null;
        (await tds.ApproveAsync(td.Id)).Code.ShouldBe("APPROVING_USER_UNKNOWN");
        As(checker);
        Ok(await tds.ApproveAsync(td.Id), "approve TD");
        As(maker);

        // 8. BILL (DEMO rules: basic 1%, SEF 1%, due 31 March, 10% prompt discount, 2%/month interest)
        var (basic, sef) = await BillingFlowTests.SeedRulesAsync(db, new DateOnly(2026, 1, 1));
        var bills = sp.GetRequiredService<IBillService>();
        var bill = Ok(await bills.GenerateAsync(new GenerateBillRequest(rpu.Id, 2026, new DateOnly(2026, 3, 15))), "generate bill");
        bill.AssessedValue.ShouldBe(100_000m);
        bill.Total.ShouldBe(1_800m);
        Ok(await bills.PostAsync(bill.Id), "post bill");

        // Collection setup (DEMO): modes, revenue accounts, receipt and transaction numbering.
        var cash = new PaymentMode { Code = $"E2EC{tag}", Name = "DEMO Cash", AllowsChange = true };
        db.Add(cash);
        foreach (var taxType in new[] { basic, sef })
        {
            foreach (var component in Enum.GetValues<BillingComponent>())
            {
                foreach (var category in Enum.GetValues<CollectionYearCategory>())
                {
                    db.Add(Approved(new RevenueAccountMapping
                    {
                        TaxTypeId = taxType.Id, Component = component, YearCategory = category,
                        AccountCode = $"DEMO-{taxType.Name}-{component}-{category}", AccountName = $"DEMO {taxType.Name} {component}", Fund = $"DEMO {taxType.Name}",
                    }));
                }
            }
        }
        db.AddRange(
            Approved(new NumberingScheme { AppliesTo = NumberedDocumentKind.OfficialReceipt, Name = "DEMO OR", Pattern = $"E2E-OR-{tag}-{{SEQ:4}}" }),
            Approved(new NumberingScheme { AppliesTo = NumberedDocumentKind.PaymentTransaction, Name = "DEMO TXN", Pattern = $"E2E-TXN-{tag}-{{SEQ:4}}" }));
        await db.SaveChangesAsync();

        // 9. PAY: part of the tax first, then everything still owed.
        var payments = sp.GetRequiredService<IPaymentService>();
        var outstanding = Ok(await payments.GetOutstandingAsync(property.Id, null), "outstanding");
        outstanding.TotalOutstandingPrincipal.ShouldBe(2_000m);
        outstanding.TotalDueAsOf.ShouldBe(1_800m);

        var part = Ok(await payments.PostAsync(new PostPaymentRequest(Guid.NewGuid().ToString(), taxpayer.Id, taxpayer.DisplayName, "DEMO Address",
            [new PaymentItemRequest(rpu.Id, 2026, 1, 500m)], [new PaymentTenderRequest(cash.Id, 500m)], 500m)), "pay part");
        var quote = Ok(await payments.QuoteAsync(new QuotePaymentRequest([new PaymentItemRequest(rpu.Id, 2026, 1)])), "quote the rest");
        quote.Total.ShouldBe(1_500m);  // no discount on the rest of a part-paid installment
        var rest = Ok(await payments.PostAsync(new PostPaymentRequest(Guid.NewGuid().ToString(), taxpayer.Id, taxpayer.DisplayName, "DEMO Address",
            [new PaymentItemRequest(rpu.Id, 2026, 1)], [new PaymentTenderRequest(cash.Id, 2_000m)], quote.Total)), "pay the rest");
        rest.Change.ShouldBe(500m);

        // 10. VERIFY BALANCE
        var after = Ok(await payments.GetOutstandingAsync(property.Id, null), "outstanding after");
        after.TotalOutstandingPrincipal.ShouldBe(0m);
        after.TotalDueAsOf.ShouldBe(0m);
        var statement = Ok(await bills.GetStatementOfAccountAsync(property.Id), "statement");
        statement.TotalPrincipalPaid.ShouldBe(2_000m);
        statement.TotalOutstandingPrincipal.ShouldBe(0m);
        statement.Payments.Select(p => p.OfficialReceiptNumber).ShouldBe([part.OfficialReceiptNumber, rest.OfficialReceiptNumber]);
        (await payments.QuoteAsync(new QuotePaymentRequest([new PaymentItemRequest(rpu.Id, 2026, 1)]))).Code.ShouldBe("PAYMENT_ALREADY_SETTLED");

        // The receipt, the remittance and the day's reconciliation close the loop.
        var receipt = Ok(await sp.GetRequiredService<IFormService>().IssueAsync(new IssueFormRequest("OFFICIAL_RECEIPT", rest.Id)), "issue receipt");
        receipt.Html!.ShouldContain(rest.OfficialReceiptNumber);
        receipt.Html.ShouldContain($"E2E-TD-{tag}");
        var reports = sp.GetRequiredService<ICollectionReportService>();
        var remittance = Ok(await reports.CreateRemittanceAsync(new CreateRemittanceRequest(null, "DEMO E2E")), "remit");
        remittance.TotalAmount.ShouldBe(2_000m);
        As(checker);
        Ok(await reports.AcceptRemittanceAsync(remittance.Id, new DecideRemittanceRequest(null)), "accept remittance");
        var mine = Ok(await reports.ReconcileAsync(null), "reconcile").Cashiers.Single(c => c.CashierUserId == maker);
        mine.AmountDue.ShouldBe(2_000m);
        mine.Remitted.ShouldBe(2_000m);
        mine.Problems.ShouldBeEmpty();
    }
}
