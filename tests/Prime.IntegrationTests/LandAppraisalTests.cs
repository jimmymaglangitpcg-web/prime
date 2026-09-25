using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Prime.Application.Features.Assessments;
using Prime.Application.Features.Lands;
using Prime.Application.Features.Smv;
using Prime.Application.Features.Valuation;
using Prime.Domain.Entities;
using Prime.Domain.Entities.Reference;
using Prime.Domain.Enums;
using Prime.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Prime.IntegrationTests;

/// <summary>
/// docs/analysis/mrpaao-forms-model.md §8 step 2b — land strips, improvements
/// and adjustment factors (MRPAAO Att. 1). Every rate, factor and level is
/// DEMO test data.
/// </summary>
public class LandAppraisalTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private sealed record Ctx(PrimeDbContext Db, IServiceProvider Services, BillingFlowTests.Seed Seed, Guid LandId, Guid SecondUseId, Guid MangoId)
    {
        public ILandService Lands => Services.GetRequiredService<ILandService>();
        public IAdjustmentFactorService Factors => Services.GetRequiredService<IAdjustmentFactorService>();
    }

    /// <summary>
    /// The seeded land (500 sqm, DEMO_Residential at 1,000/sqm, level 20%), plus:
    /// a second use at 400/sqm with a 10% level; a DEMO mango rate of 2,000 per
    /// tree under the first use.
    /// </summary>
    private async Task<(Ctx Ctx, IAsyncDisposable Transaction)> BeginAsync()
    {
        var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PrimeDbContext>();
        var transaction = await db.Database.BeginTransactionAsync();
        var seed = await BillingFlowTests.SeedPostedAssessmentAsync(scope.ServiceProvider, db, new DateOnly(2026, 1, 1));
        var landType = await TestSeed.LandPropertyTypeAsync(db);
        var secondUse = new ActualUse { Code = $"AU{Guid.NewGuid():N}"[..8], Name = "DEMO_Second Use" };
        var mango = new ImprovementKind { Code = $"IK{Guid.NewGuid():N}"[..8], Name = "DEMO_Mango" };
        db.AddRange(secondUse, mango);
        db.AssessmentLevels.Add(new AssessmentLevel
        {
            OrdinanceNumber = $"ORD-{Guid.NewGuid():N}"[..20], ClassificationId = seed.ClassificationId, ActualUseId = secondUse.Id,
            PropertyTypeId = landType.Id, LowerValue = 0m, AssessmentPercentage = 10m, EffectiveDate = new DateOnly(2026, 1, 1),
            Status = WorkflowStatus.Approved, ApprovedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();

        var smv = scope.ServiceProvider.GetRequiredService<ISmvService>();
        foreach (var request in new[]
        {
            new CreateSmvScheduleRequest(seed.ClassificationId, secondUse.Id, landType.Id, null, "per sqm", 400m, null, null, new DateOnly(2026, 1, 1)),
            new CreateSmvScheduleRequest(seed.ClassificationId, seed.TaxDeclaration.ActualUseId, landType.Id, null, "per tree", 2_000m, null, null,
                new DateOnly(2026, 1, 1), mango.Id),
        })
        {
            var schedule = await smv.CreateScheduleAsync(seed.SmvId, request);
            schedule.IsSuccess.ShouldBeTrue(schedule.IsSuccess ? null : schedule.Message);
            (await smv.ApproveScheduleAsync(schedule.Value.Id)).IsSuccess.ShouldBeTrue();
        }
        var landId = await db.Lands.Where(x => x.RpuId == seed.RpuId).Select(x => x.Id).SingleAsync();
        return (new Ctx(db, scope.ServiceProvider, seed, landId, secondUse.Id, mango.Id), new Scoped(transaction, scope));
    }

    private sealed class Scoped(IDbContextTransaction transaction, IServiceScope scope) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            await transaction.DisposeAsync();
            scope.Dispose();
        }
    }

    private static async Task ApprovedFactorAsync(Ctx c, Guid smvId, string code, decimal percent)
    {
        var created = await c.Factors.CreateAsync(new CreateAdjustmentFactorRequest(
            smvId, code, $"DEMO {code}", percent, null, null, "DEMO — not an ordinance", new DateOnly(2026, 1, 1), null));
        created.IsSuccess.ShouldBeTrue(created.IsSuccess ? null : created.Message);
        (await c.Factors.ApproveAsync(created.Value.Id)).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task StripsImprovementsAndAdjustments_ValueAsSeparateRows_AndAssessByUse()
    {
        var (c, tx) = await BeginAsync();
        await using var _ = tx;
        var code = $"C{Guid.NewGuid():N}"[..8];
        await ApprovedFactorAsync(c, c.Seed.SmvId, code, 10m);

        // The seeded land has no strips: adding one keeps its 500 sqm as strip 1.
        var land = (await c.Lands.AddStripAsync(c.LandId, new AddLandStripRequest(c.Seed.ClassificationId, null, c.SecondUseId, null, 300m))).Value;
        land.Strips.Select(s => (s.Sequence, s.Area)).ShouldBe([(1, 500m), (2, 300m)]);
        land.Area.ShouldBe(800m);
        land.ActualUseId.ShouldBe(c.Seed.TaxDeclaration.ActualUseId); // principal: the larger strip
        (await c.Lands.AddImprovementAsync(c.LandId, new AddLandImprovementRequest(c.MangoId, 10m, true, null, null, null))).IsSuccess.ShouldBeTrue();
        var withAdjustment = await c.Lands.AddAdjustmentAsync(c.LandId, new AddLandAdjustmentRequest(code, land.Strips[0].Id, "DEMO"));
        withAdjustment.IsSuccess.ShouldBeTrue(withAdjustment.IsSuccess ? null : withAdjustment.Message);

        var valuation = await c.Services.GetRequiredService<IValuationService>().ComputeForRpuAsync(c.Seed.RpuId);

        valuation.IsSuccess.ShouldBeTrue(valuation.IsSuccess ? null : valuation.Message);
        valuation.Value.ComputedMarketValue.ShouldBe(690_000m);
        var lines = await c.Db.ValuationLines.Where(x => x.ValuationId == valuation.Value.Id).OrderBy(x => x.Sequence).ToListAsync();
        lines.Select(l => (l.Source, l.MarketValue)).ShouldBe([
            (ValuationLineSource.LandStrip, 550_000m),       // 500 × 1,000 + 10%
            (ValuationLineSource.LandStrip, 120_000m),       // 300 × 400
            (ValuationLineSource.LandImprovement, 20_000m),  // 10 × 2,000
        ]);
        lines[0].BreakdownJson.ShouldContain(code);
        lines[2].Description.ShouldBe("DEMO_Mango (productive)");

        var assessment = await c.Services.GetRequiredService<IAssessmentService>().CreateAsync(
            new CreateAssessmentRequest(valuation.Value.Id, 2026, new DateOnly(2026, 1, 1), null, null, "DEMO"));
        assessment.IsSuccess.ShouldBeTrue(assessment.IsSuccess ? null : assessment.Message);
        assessment.Value.Lines.Select(l => (l.ActualUseName, l.MarketValue, l.AssessedValue)).ShouldBe([
            ("DEMO_Residential Use", 570_000m, 114_000m), // strip 1 + the mango trees, at 20%
            ("DEMO_Second Use", 120_000m, 12_000m),       // at 10%
        ]);
        assessment.Value.AssessedValue.ShouldBe(126_000m);
    }

    [Fact]
    public async Task UnknownFactorCode_IsRefused()
    {
        var (c, tx) = await BeginAsync();
        await using var _ = tx;

        (await c.Lands.AddAdjustmentAsync(c.LandId, new AddLandAdjustmentRequest($"X{Guid.NewGuid():N}"[..8], null, null)))
            .Code.ShouldBe("ADJUSTMENT_FACTOR_UNKNOWN");
    }

    [Fact]
    public async Task FactorNotInForceUnderTheStripsSmv_StopsTheValuation()
    {
        var (c, tx) = await BeginAsync();
        await using var _ = tx;
        var otherSmv = (await c.Services.GetRequiredService<ISmvService>().CreateSmvAsync(new CreateSmvRequest(
            $"ORD-{Guid.NewGuid():N}", new DateOnly(2025, 1, 1), new DateOnly(2025, 1, 15), new DateOnly(2025, 1, 1), 2025, "DEMO other SMV"))).Value;
        var code = $"R{Guid.NewGuid():N}"[..8];
        await ApprovedFactorAsync(c, otherSmv.Id, code, 5m);
        (await c.Lands.AddAdjustmentAsync(c.LandId, new AddLandAdjustmentRequest(code, null, null))).IsSuccess.ShouldBeTrue();

        var valuation = await c.Services.GetRequiredService<IValuationService>().ComputeForRpuAsync(c.Seed.RpuId);

        valuation.Code.ShouldBe("ADJUSTMENT_FACTOR_NOT_FOUND");
        valuation.Message.ShouldNotBeNull().ShouldContain(code);
    }

    [Fact]
    public async Task NewLand_StartsWithOneStrip_FromItsRegistration()
    {
        var (c, tx) = await BeginAsync();
        await using var _ = tx;
        var rpu = new RealPropertyUnit { PropertyId = c.Seed.PropertyId, RpuNumber = $"RPU-{Guid.NewGuid():N}", RpuType = RpuType.Land, EffectivityDate = new DateOnly(2026, 1, 1) };
        c.Db.Add(rpu);
        await c.Db.SaveChangesAsync();

        var land = await c.Lands.CreateAsync(new CreateLandRequest(rpu.Id, 250m, "sqm", c.Seed.ClassificationId, c.SecondUseId, null, null, null, null, null, false, null));

        land.IsSuccess.ShouldBeTrue(land.IsSuccess ? null : land.Message);
        var strip = land.Value.Strips.ShouldHaveSingleItem();
        strip.Area.ShouldBe(250m);
        strip.ActualUseId.ShouldBe(c.SecondUseId);
    }
}
