using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Prime.Application.Common.Interfaces;
using Prime.Application.Features.Appraisal;
using Prime.Application.Features.Assessments;
using Prime.Application.Features.Forms;
using Prime.Application.Features.Numbering;
using Prime.Application.Features.Taxpayers;
using Prime.Domain.Entities;
using Prime.Domain.Entities.Identity;
using Prime.Domain.Entities.Reference;
using Prime.Domain.Enums;
using Prime.Infrastructure.Identity;
using Prime.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Prime.IntegrationTests;

/// <summary>
/// docs/FORMS-REVISION-PLAN.md A7 — the appraisal record (FAAS aggregate).
/// Everything here is DEMO test data.
/// </summary>
public class AppraisalRecordTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private sealed record Ctx(PrimeDbContext Db, IServiceProvider Services, BillingFlowTests.Seed Seed, DateOnly Today)
    {
        public IAppraisalRecordService Appraisals => Services.GetRequiredService<IAppraisalRecordService>();
    }

    private async Task<(Ctx Ctx, IAsyncDisposable Transaction)> BeginAsync(bool post = true)
    {
        var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PrimeDbContext>();
        var transaction = await db.Database.BeginTransactionAsync();
        await db.NumberingSchemes.Where(x => x.Status == WorkflowStatus.Approved)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, WorkflowStatus.Cancelled).SetProperty(x => x.ApprovedAt, (DateTimeOffset?)null));
        await db.ApprovalChains.Where(x => x.Status == WorkflowStatus.Approved)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, WorkflowStatus.Cancelled).SetProperty(x => x.ApprovedAt, (DateTimeOffset?)null));
        var seed = await BillingFlowTests.SeedPostedAssessmentAsync(scope.ServiceProvider, db, new DateOnly(2026, 1, 1), post: post);
        var today = scope.ServiceProvider.GetRequiredService<IClock>().Today;
        return (new Ctx(db, scope.ServiceProvider, seed, today), new Scoped(transaction, scope));
    }

    private sealed class Scoped(IDbContextTransaction transaction, IServiceScope scope) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            await transaction.DisposeAsync();
            scope.Dispose();
        }
    }

    private static async Task AddPartyAsync(Ctx c, string lastName, DateOnly start, decimal share = 100m)
    {
        var owner = new Taxpayer { TaxpayerType = TaxpayerType.Individual, LastName = lastName, FirstName = "Juan", Address = "DEMO Address 1" };
        var type = new OwnershipType { Code = $"OT{Guid.NewGuid():N}"[..8], Name = "DEMO_Sole" };
        c.Db.AddRange(owner, type);
        await c.Db.SaveChangesAsync();
        (await c.Services.GetRequiredService<ITaxpayerService>().AddOwnerAsync(
            new AddPropertyOwnerRequest(c.Seed.PropertyId, owner.Id, type.Id, share, start))).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task LandRecord_AssemblesPropertyPartiesTdAssetValuationAndAssessment()
    {
        var (c, tx) = await BeginAsync();
        await using var _ = tx;
        await AddPartyAsync(c, "DEMO_Owner", new DateOnly(2020, 1, 1), 50m);
        await AddPartyAsync(c, "DEMO_LaterOwner", new DateOnly(2026, 6, 1), 50m); // after the effective date: not on this record

        var result = await c.Appraisals.GetAsync(c.Seed.AssessmentId);

        result.IsSuccess.ShouldBeTrue(result.IsSuccess ? null : result.Message);
        var r = result.Value;
        r.Kind.ShouldBe(ValuationSourceType.Land);
        r.Status.ShouldBe(WorkflowStatus.Posted);
        r.FaasNumber.ShouldBeNull(); // no FAAS scheme in force
        r.Property.Barangay.ShouldBe("Demo Barangay");
        r.PartiesAsOf.ShouldBe(new DateOnly(2026, 1, 1));
        r.Parties.Select(p => p.Name).ShouldHaveSingleItem().ShouldContain("DEMO_Owner");
        r.TaxDeclaration.ShouldNotBeNull().Number.ShouldBe(c.Seed.TaxDeclaration.TaxDeclarationNumber);
        r.Land.ShouldNotBeNull().Area.ShouldBe(500m);
        r.Land.Classification.ShouldBe("DEMO_Residential");
        r.Building.ShouldBeNull();
        r.Machinery.ShouldBeNull();

        r.Valuation.Smv.ShouldNotBeNull().Id.ShouldBe(c.Seed.SmvId);
        r.Valuation.ScheduleRate.ShouldBe(1000m);
        r.Valuation.MarketValue.ShouldBe(500_000m);
        var lines = r.Valuation.Breakdown.ToDictionary(l => l.Key, l => l.Value);
        lines["Area"].ShouldBe(500m);
        lines["Rate"].ShouldBe(1000m);
        lines["MarketValue"].ShouldBe(500_000m);
        r.Valuation.Breakdown.Select(l => l.Key).ShouldBe(["Area", "Rate", "LocationFactor", "BaseValue", "ValueBeforeClamp", "MarketValue"]);

        r.Assessment.AssessmentLevelPercent.ShouldBe(20m);
        r.Assessment.LevelLowerValue.ShouldBe(0m);
        r.Assessment.LevelUpperValue.ShouldBe(1_000_000m);
        r.Assessment.AssessedValue.ShouldBe(100_000m);
        r.Assessment.Classification.ShouldBe("DEMO_Residential");
        r.Previous.ShouldBeNull();
        r.Signatures.ShouldBeEmpty(); // the DEMO seed sets Posted directly, with no approval
        r.Notices.ShouldBeEmpty();
    }

    [Fact]
    public async Task Reassessment_ShowsThePreviousAssessmentAndTheChange()
    {
        var (c, tx) = await BeginAsync();
        await using var _ = tx;
        var first = await c.Db.Assessments.SingleAsync(x => x.Id == c.Seed.AssessmentId);
        var created = (await c.Services.GetRequiredService<IAssessmentService>().CreateAsync(
            new CreateAssessmentRequest(first.ValuationId, 2027, new DateOnly(2027, 1, 1), first.Id, null, "DEMO reassessment"))).Value;
        var second = await c.Db.Assessments.SingleAsync(x => x.Id == created.Id);
        second.AssessedValue = 120_000m; // stands in for a new valuation
        await c.Db.SaveChangesAsync();

        var r = (await c.Appraisals.GetAsync(second.Id)).Value;

        r.Status.ShouldBe(WorkflowStatus.Draft);
        var previous = r.Previous.ShouldNotBeNull();
        previous.AssessmentId.ShouldBe(first.Id);
        previous.AssessedValue.ShouldBe(100_000m);
        previous.AssessedValueChange.ShouldBe(20_000m);
    }

    [Fact]
    public async Task Approval_NumbersTheFaas_AndTheRecordCarriesSignerAndNumber()
    {
        var (c, tx) = await BeginAsync(post: false);
        await using var _ = tx;
        var user = c.Services.GetRequiredService<CurrentUserService>();
        var users = Enumerable.Range(0, 2).Select(i => new AppUser
        {
            SupabaseUserId = Guid.NewGuid(), DisplayName = $"DEMO Signer {(char)('A' + i)}", Email = $"demo-{Guid.NewGuid():N}@example.invalid",
        }).ToList();
        c.Db.AppUsers.AddRange(users);
        await c.Db.SaveChangesAsync();

        var numbering = c.Services.GetRequiredService<INumberingService>();
        user.AppUserId = users[0].Id;
        var scheme = (await numbering.CreateAsync(new CreateNumberingSchemeRequest(
            "DEMO — not an LGU format", c.Today, null, NumberedDocumentKind.Faas, "DEMO FAAS", "DEMO-FAAS-{YEAR}-{SEQ:4}", null, false))).Value;
        user.AppUserId = users[1].Id;
        (await numbering.ApproveAsync(scheme.Id)).IsSuccess.ShouldBeTrue();

        // The seeded draft was created with no acting user; A submits, B approves (maker-checker, no chain in force).
        var assessments = c.Services.GetRequiredService<IAssessmentService>();
        user.AppUserId = users[0].Id;
        (await assessments.SubmitForReviewAsync(c.Seed.AssessmentId)).IsSuccess.ShouldBeTrue();
        (await c.Appraisals.GetAsync(c.Seed.AssessmentId)).Value.FaasNumber.ShouldBeNull(); // numbered on approval, not before
        user.AppUserId = users[1].Id;
        var approved = await assessments.ApproveAsync(c.Seed.AssessmentId);
        approved.IsSuccess.ShouldBeTrue(approved.IsSuccess ? null : approved.Message);
        approved.Value.FaasNumber.ShouldBe("DEMO-FAAS-2026-0001");

        var r = (await c.Appraisals.GetAsync(c.Seed.AssessmentId)).Value;
        r.FaasNumber.ShouldBe("DEMO-FAAS-2026-0001");
        var signature = r.Signatures.ShouldHaveSingleItem();
        signature.Label.ShouldBe("Approved by");
        signature.Name.ShouldBe("DEMO Signer B");

        // The form data provider serves the same record, issuable once approved.
        var provider = c.Services.GetServices<IFormDataProvider>().Single(p => p.SubjectType == FormSubjectType.Assessment);
        var data = (await provider.BuildAsync(c.Seed.AssessmentId, CancellationToken.None)).ShouldNotBeNull();
        data.IssueBlocker.ShouldBeNull();
        data.DocumentNumber.ShouldBe("DEMO-FAAS-2026-0001");
        data.Data["appraisal"]!["kind"]!.GetValue<string>().ShouldBe("Land");
        data.Data["appraisal"]!["assessment"]!["assessedValue"]!.GetValue<decimal>().ShouldBe(100_000m);
    }

    [Fact]
    public async Task DraftRecord_PreviewsButIsNotIssuable_AndUnknownIdIsNotFound()
    {
        var (c, tx) = await BeginAsync(post: false);
        await using var _ = tx;
        var provider = c.Services.GetServices<IFormDataProvider>().Single(p => p.SubjectType == FormSubjectType.Assessment);

        var draft = (await provider.BuildAsync(c.Seed.AssessmentId, CancellationToken.None)).ShouldNotBeNull();
        draft.IssueBlocker.ShouldNotBeNull().ShouldContain("Draft");
        (await provider.BuildAsync(Guid.NewGuid(), CancellationToken.None)).ShouldBeNull();
        (await c.Appraisals.GetAsync(Guid.NewGuid())).Code.ShouldBe("ASSESSMENT_NOT_FOUND");
    }
}
