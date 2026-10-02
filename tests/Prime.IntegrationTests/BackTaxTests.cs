using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Prime.Application.Features.AssessmentLevels;
using Prime.Application.Features.Assessments;
using Prime.Application.Features.Smv;
using Prime.Domain.Entities;
using Prime.Domain.Entities.Reference;
using Prime.Domain.Entities.Transactions;
using Prime.Domain.Enums;
using Prime.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Prime.IntegrationTests;

/// <summary>
/// Step L1-8 (docs/analysis/valuation-foundation.md §4.8; exit criterion 1): back taxes with the same structure as
/// the LAM's worked example (Bk III pp.79–80: five SMV periods, one level, the ten-year limit), in DEMO figures —
/// the LAM's own figures are not reproduced (CLAUDE.md §118, Q16).
/// </summary>
public class BackTaxTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private sealed record Ctx(PrimeDbContext Db, IServiceProvider Services, Guid RpuId)
    {
        public IBackTaxService BackTaxes => Services.GetRequiredService<IBackTaxService>();
        public IAssessmentService Assessments => Services.GetRequiredService<IAssessmentService>();
    }

    /// <summary>
    /// A DEMO land of 500 sqm in its own town, with an approved TD, five DEMO SMVs covering the town (effective 2014,
    /// 2017, 2019, 2021 and 2024 at 100, 150, 200, 300 and 400 per sqm) and one DEMO level of 20 %.
    /// </summary>
    private async Task<(Ctx Ctx, IAsyncDisposable Transaction)> BeginAsync()
    {
        var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PrimeDbContext>();
        var transaction = await db.Database.BeginTransactionAsync();
        var province = new Province { PsgcCode = $"P{Guid.NewGuid():N}"[..10], Name = "DEMO Province" };
        var town = new Municipality { Province = province, PsgcCode = $"M{Guid.NewGuid():N}"[..10], Name = "DEMO Town" };
        var barangay = new Barangay { Municipality = town, PsgcCode = $"B{Guid.NewGuid():N}"[..10], Name = "DEMO Barangay" };
        var classification = new Classification { Code = $"CL{Guid.NewGuid():N}"[..8], Name = "DEMO_Residential" };
        var use = new ActualUse { Code = $"AU{Guid.NewGuid():N}"[..8], Name = "DEMO_Residential Use" };
        var landType = await TestSeed.LandPropertyTypeAsync(db);
        var property = new PropertyEntity { PropertyIdentificationNumber = $"PIN-{Guid.NewGuid():N}", Province = province, Municipality = town, Barangay = barangay };
        var rpu = new RealPropertyUnit { Property = property, RpuNumber = $"RPU-{Guid.NewGuid():N}", RpuType = RpuType.Land, EffectivityDate = new DateOnly(2025, 1, 1) };
        db.AddRange(province, town, barangay, classification, use, property, rpu,
            new Land { Rpu = rpu, Property = property, Area = 500m, Classification = classification, ActualUse = use },
            new TaxDeclaration
            {
                Rpu = rpu, Property = property, TaxDeclarationNumber = $"TD-{Guid.NewGuid():N}", EffectivityDate = new DateOnly(2025, 1, 1),
                Taxability = Taxability.Taxable, Classification = classification, ActualUse = use, AssessmentYear = 2025, Status = WorkflowStatus.Approved,
                ApprovedAt = DateTimeOffset.UtcNow,
            });
        await db.SaveChangesAsync();

        var smv = scope.ServiceProvider.GetRequiredService<ISmvService>();
        foreach (var (year, rate) in new[] { (2014, 100m), (2017, 150m), (2019, 200m), (2021, 300m), (2024, 400m) })
        {
            var effective = new DateOnly(year, 1, 1);
            var created = await smv.CreateSmvAsync(new CreateSmvRequest($"DEMO-BT-{year}-{Guid.NewGuid():N}"[..30], effective.AddMonths(-6), effective.AddMonths(-3),
                effective, year, "DEMO SMV for BackTaxTests", MunicipalityIds: [town.Id]));
            created.IsSuccess.ShouldBeTrue(created.IsSuccess ? null : created.Message);
            (await smv.ApproveSmvAsync(created.Value.Id)).IsSuccess.ShouldBeTrue();
            var schedule = await smv.CreateScheduleAsync(created.Value.Id, new CreateSmvScheduleRequest(classification.Id, use.Id, landType.Id, null, "per sqm", rate, null, null, effective));
            schedule.IsSuccess.ShouldBeTrue(schedule.IsSuccess ? null : schedule.Message);
            (await smv.ApproveScheduleAsync(schedule.Value.Id)).IsSuccess.ShouldBeTrue();
        }
        var levels = scope.ServiceProvider.GetRequiredService<IAssessmentLevelService>();
        var level = (await levels.CreateAsync(new CreateAssessmentLevelRequest($"DEMO-BT-{Guid.NewGuid():N}"[..20], new DateOnly(2009, 1, 1),
            classification.Id, use.Id, landType.Id, 0m, null, 20m, new DateOnly(2010, 1, 1)))).Value;
        (await levels.ApproveAsync(level.Id)).IsSuccess.ShouldBeTrue();
        return (new Ctx(db, scope.ServiceProvider, rpu.Id), new Scoped(transaction, scope));
    }

    private sealed class Scoped(IDbContextTransaction transaction, IServiceScope scope) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            await transaction.DisposeAsync();
            scope.Dispose();
        }
    }

    private static BackTaxRequest Request(Guid rpuId, int from = 2016) => new(rpuId, from, "DEMO certificate of completion dated 2015-12", 2025);

    [Fact]
    public async Task FivePeriods_EachValuedUnderItsOwnSmv_AndAssessedAtItsLevel()
    {
        var (c, tx) = await BeginAsync();
        await using var _ = tx;

        var preview = await c.BackTaxes.PreviewAsync(Request(c.RpuId));
        preview.IsSuccess.ShouldBeTrue(preview.IsSuccess ? null : preview.Message);
        preview.Value.Periods.Select(p => (p.StartDate, p.EndDate)).ShouldBe([
            (new DateOnly(2016, 1, 1), (DateOnly?)new DateOnly(2016, 12, 31)), (new DateOnly(2017, 1, 1), new DateOnly(2018, 12, 31)),
            (new DateOnly(2019, 1, 1), new DateOnly(2020, 12, 31)), (new DateOnly(2021, 1, 1), new DateOnly(2023, 12, 31)), (new DateOnly(2024, 1, 1), null),
        ]);
        (await c.Db.Assessments.CountAsync(x => x.RpuId == c.RpuId)).ShouldBe(0); // a preview writes nothing

        var run = await c.BackTaxes.CreateAsync(Request(c.RpuId));

        run.IsSuccess.ShouldBeTrue(run.IsSuccess ? null : run.Message);
        run.Value.Periods.Select(p => (p.MarketValue, p.AssessedValue, p.AssessmentStatus)).ShouldBe([
            (50_000m, 10_000m, WorkflowStatus.Draft), (75_000m, 15_000m, WorkflowStatus.Draft), (100_000m, 20_000m, WorkflowStatus.Draft),
            (150_000m, 30_000m, WorkflowStatus.Draft), (200_000m, 40_000m, WorkflowStatus.Draft),
        ]);
        run.Value.Periods.Select(p => p.SmvReference![..12]).ShouldBe(["DEMO-BT-2014", "DEMO-BT-2017", "DEMO-BT-2019", "DEMO-BT-2021", "DEMO-BT-2024"]);
        var assessments = await c.Db.Assessments.Where(x => x.RpuId == c.RpuId).OrderBy(x => x.EffectiveDate).ToListAsync();
        assessments.Select(a => (a.EffectiveDate, a.AssessmentYear)).ShouldBe([
            (new DateOnly(2016, 1, 1), 2016), (new DateOnly(2017, 1, 1), 2017), (new DateOnly(2019, 1, 1), 2019), (new DateOnly(2021, 1, 1), 2021),
            (new DateOnly(2024, 1, 1), 2024),
        ]);
        (await c.BackTaxes.ListByRpuAsync(c.RpuId)).Value.Single().Basis.ShouldBe("DEMO certificate of completion dated 2015-12");
    }

    [Fact]
    public async Task Periods_ArePostedInOrder_EachPreparingItsTaxDeclaration()
    {
        var (c, tx) = await BeginAsync();
        await using var _ = tx;
        // A DEMO TD numbering scheme in force, so posting prepares each period's TD.
        var numbering = c.Services.GetRequiredService<Prime.Application.Features.Numbering.INumberingService>();
        var scheme = await numbering.CreateAsync(new Prime.Application.Features.Numbering.CreateNumberingSchemeRequest(
            "DEMO — not an LGU format", DateOnly.FromDateTime(DateTime.Today), null, NumberedDocumentKind.TaxDeclaration, "DEMO TD", "DEMO-BT-{YEAR}-{SEQ:6}", null, false));
        scheme.IsSuccess.ShouldBeTrue(scheme.IsSuccess ? null : scheme.Message);
        (await numbering.ApproveAsync(scheme.Value.Id)).IsSuccess.ShouldBeTrue();
        var run = (await c.BackTaxes.CreateAsync(Request(c.RpuId, from: 2023))).Value; // 2023 (2021 SMV) and 2024
        run.Periods.Count.ShouldBe(2);
        foreach (var p in run.Periods)
        {
            (await c.Assessments.SubmitForReviewAsync(p.AssessmentId!.Value)).IsSuccess.ShouldBeTrue();
            var approved = await c.Assessments.ApproveAsync(p.AssessmentId!.Value);
            approved.IsSuccess.ShouldBeTrue(approved.IsSuccess ? null : approved.Message);
        }

        (await c.Assessments.PostAsync(run.Periods[1].AssessmentId!.Value)).Code.ShouldBe("BACK_TAX_PERIOD_ORDER");
        var first = await c.Assessments.PostAsync(run.Periods[0].AssessmentId!.Value);
        first.IsSuccess.ShouldBeTrue(first.IsSuccess ? null : first.Message);
        var firstTd = await c.Db.TaxDeclarations.SingleAsync(x => x.AssessmentId == run.Periods[0].AssessmentId);
        (firstTd.EffectivityDate, firstTd.Status).ShouldBe((new DateOnly(2023, 1, 1), WorkflowStatus.Draft));
        firstTd.Remarks.ShouldContain("Back-tax period 1 of 2, to 2023-12-31.");
        // The period's TD is approved before the next period's is prepared from it.
        var tds = c.Services.GetRequiredService<Prime.Application.Features.TaxDeclarations.ITaxDeclarationService>();
        (await tds.SubmitForReviewAsync(firstTd.Id)).IsSuccess.ShouldBeTrue();
        var approvedTd = await tds.ApproveAsync(firstTd.Id);
        approvedTd.IsSuccess.ShouldBeTrue(approvedTd.IsSuccess ? null : approvedTd.Message);
        (await c.Assessments.PostAsync(run.Periods[1].AssessmentId!.Value)).IsSuccess.ShouldBeTrue();
        var currentTd = await c.Db.TaxDeclarations.SingleAsync(x => x.AssessmentId == run.Periods[1].AssessmentId);
        currentTd.PreviousTaxDeclarationId.ShouldBe(firstTd.Id);
        currentTd.Remarks.ShouldContain("Current period of the back taxes from 2023 (periods from 2023-01-01, 2024-01-01).");
    }

    [Fact]
    public async Task TheStart_IsLimited_AndOnlyAFirstDeclarationIsBackTaxed()
    {
        var (c, tx) = await BeginAsync();
        await using var _ = tx;
        (await c.BackTaxes.PreviewAsync(Request(c.RpuId, from: 2014))).Code.ShouldBe("BACK_TAX_START_INVALID"); // 11 years before 2025
        (await c.BackTaxes.PreviewAsync(Request(c.RpuId, from: 2026))).Code.ShouldBe("BACK_TAX_START_INVALID");
        (await c.BackTaxes.PreviewAsync(Request(c.RpuId) with { Basis = " " })).Code.ShouldBe("VALIDATION_FAILED");

        var nextJanuary = new TransactionType
        {
            Code = $"T{Guid.NewGuid():N}"[..12], Name = "DEMO", Kind = PropertyTransactionKind.NewAssessment, LegalBasis = "DEMO",
            EffectiveDate = new DateOnly(2020, 1, 1), Status = WorkflowStatus.Approved, ApprovedAt = DateTimeOffset.UtcNow,
            EffectivityRule = EffectivityRule.NextJanuary, EffectivityLegalBasis = "DEMO",
        };
        c.Db.TransactionTypes.Add(nextJanuary);
        await c.Db.SaveChangesAsync();
        (await c.BackTaxes.PreviewAsync(Request(c.RpuId) with { TransactionTypeId = nextJanuary.Id })).Code.ShouldBe("BACK_TAX_TRANSACTION_RULE");

        var run = (await c.BackTaxes.CreateAsync(Request(c.RpuId, from: 2024))).Value;
        var a = await c.Db.Assessments.SingleAsync(x => x.Id == run.Periods.Single().AssessmentId);
        a.Status = WorkflowStatus.Posted;
        await c.Db.SaveChangesAsync();
        (await c.BackTaxes.PreviewAsync(Request(c.RpuId))).Code.ShouldBe("BACK_TAX_ALREADY_ASSESSED");
    }
}
