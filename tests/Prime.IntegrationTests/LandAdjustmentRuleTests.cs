using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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
/// Step L1-4 (docs/analysis/valuation-foundation.md §4.4): factor rule kinds read the land's road,
/// corner status, distance and the strip's depth band; depth never applies to subdivision lots;
/// market values round to a configured step; a separately owned improvement unit is valued apart
/// from its land. DEMO SMV (1,000/sqm on 500 sqm) and DEMO percentages; rolled back.
/// </summary>
public class LandAdjustmentRuleTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly DateOnly On = new(2026, 1, 1);

    private sealed record Context(IServiceProvider Services, PrimeDbContext Db, BillingFlowTests.Seed Seed, Land Land, RoadType Dirt, IAsyncDisposable Scope);

    private async Task<Context> BeginAsync(WebApplicationFactory<Program>? host = null)
    {
        var scope = (host ?? factory).Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<PrimeDbContext>();
        var transaction = await db.Database.BeginTransactionAsync();
        var seed = await BillingFlowTests.SeedPostedAssessmentAsync(services, db, On);
        var land = await db.Lands.AsNoTracking().SingleAsync(x => x.RpuId == seed.RpuId);
        var paved = new RoadType { Code = $"RP{Guid.NewGuid():N}"[..8], Name = "DEMO paved" };
        var dirt = new RoadType { Code = $"RD{Guid.NewGuid():N}"[..8], Name = "DEMO dirt" };
        db.AddRange(paved, dirt);
        await db.SaveChangesAsync();

        var factors = services.GetRequiredService<IAdjustmentFactorService>();
        async Task Factor(string code, AdjustmentRuleKind kind, decimal percent = 0m, DistanceReference? reference = null, decimal? depth = null,
            Guid? classificationId = null, params AdjustmentFactorRowRequest[] rows)
        {
            var created = await factors.CreateAsync(new CreateAdjustmentFactorRequest(seed.SmvId, code, $"DEMO {code}", percent, classificationId, null,
                "DEMO — not an SMV provision", On, null, kind, reference, depth, rows));
            created.IsSuccess.ShouldBeTrue(created.Message);
            (await TestSeed.AsCheckerAsync(services, () => factors.ApproveAsync(created.Value.Id))).IsSuccess.ShouldBeTrue();
        }
        await Factor("L14-ROAD", AdjustmentRuleKind.ByRoadType, rows: [new(paved.Id, null, null, null, 0m), new(dirt.Id, null, null, null, -10m)]);
        await Factor("L14-KM", AdjustmentRuleKind.ByDistance, reference: DistanceReference.Poblacion,
            rows: [new(null, null, 2m, null, 0m), new(null, 2m, 5m, null, -5m), new(null, 5m, null, null, -10m)]);
        await Factor("L14-CORNER", AdjustmentRuleKind.Corner, 15m);
        await Factor("L14-DEPTH", AdjustmentRuleKind.Depth, depth: 20m, classificationId: seed.ClassificationId,
            rows: [new(null, null, null, 1, -20m), new(null, null, null, 2, -40m)]);

        var lands = services.GetRequiredService<ILandService>();
        (await lands.UpdateAppraisalInputsAsync(land.Id, new UpdateLandAppraisalInputsRequest(dirt.Id, 20m, true, null, 3m, false, "DEMO survey"))).IsSuccess.ShouldBeTrue();
        // Strip 1: the registered 500 sqm (no depth band); strip 2: 100 sqm in depth band 1.
        (await lands.AddStripAsync(land.Id, new AddLandStripRequest(seed.ClassificationId, null, land.ActualUseId, null, 100m, DepthBand: 1))).IsSuccess.ShouldBeTrue();
        foreach (var code in new[] { "L14-ROAD", "L14-KM", "L14-CORNER", "L14-DEPTH" })
        {
            var added = await lands.AddAdjustmentAsync(land.Id, new AddLandAdjustmentRequest(code, null, null));
            added.IsSuccess.ShouldBeTrue(added.Message);
        }
        return new Context(services, db, seed, land, dirt, new Disposer(transaction, scope));
    }

    private sealed class Disposer(IAsyncDisposable transaction, IAsyncDisposable scope) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            await transaction.DisposeAsync();
            await scope.DisposeAsync();
        }
    }

    [Fact]
    public async Task Each_rule_takes_its_percentage_from_the_land()
    {
        var c = await BeginAsync();
        await using var _ = c.Scope;
        var valued = await c.Services.GetRequiredService<IValuationService>().ComputeForLandAsync(c.Land.Id, asOf: On);
        valued.IsSuccess.ShouldBeTrue(valued.Message);
        var lines = valued.Value.Lines!;
        // Strip 1: dirt road -10, 3 km to the poblacion -5, corner +15 → 0 %; no depth band.
        lines[0].MarketValue.ShouldBe(500_000m);
        lines[0].Breakdown.Single(b => b.Key == "AdjustmentPercent").Value.ShouldBe(0m);
        lines[0].Breakdown.ShouldNotContain(b => b.Key == "Adjustment:L14-DEPTH");
        // Strip 2: the same, plus depth band 1 at -20 → -20 % of 100,000.
        lines[1].MarketValue.ShouldBe(80_000m);
        lines[1].Breakdown.Single(b => b.Key == "Adjustment:L14-DEPTH").Value.ShouldBe(-20m);
        valued.Value.ComputedMarketValue.ShouldBe(580_000m);
    }

    [Fact]
    public async Task A_factor_that_cannot_apply_stops_the_valuation_with_the_reason()
    {
        var c = await BeginAsync();
        await using var _ = c.Scope;
        var lands = c.Services.GetRequiredService<ILandService>();
        var valuations = c.Services.GetRequiredService<IValuationService>();
        async Task<string> Refusal(UpdateLandAppraisalInputsRequest inputs)
        {
            (await lands.UpdateAppraisalInputsAsync(c.Land.Id, inputs)).IsSuccess.ShouldBeTrue();
            var result = await valuations.ComputeForLandAsync(c.Land.Id, asOf: On);
            result.Code.ShouldBe("ADJUSTMENT_NOT_APPLICABLE");
            return result.Message!;
        }
        (await Refusal(new(c.Dirt.Id, 20m, true, null, 3m, true, "DEMO: a subdivision lot"))).ShouldContain("subdivision lots");
        (await Refusal(new(c.Dirt.Id, 20m, false, null, 3m, false, "DEMO: not a corner"))).ShouldContain("corner lot");
        (await Refusal(new(null, 20m, true, null, 3m, false, "DEMO: road unknown"))).ShouldContain("road type");
        (await Refusal(new(c.Dirt.Id, 20m, true, null, null, false, "DEMO: distance unknown"))).ShouldContain("poblacion");

        // The change of inputs is audited with its reason.
        (await c.Db.Set<Prime.Domain.Entities.Audit.AuditLog>().AnyAsync(a => a.RecordId == c.Land.Id && a.Reason == "DEMO: distance unknown")).ShouldBeTrue();
        // A reason is required.
        (await lands.UpdateAppraisalInputsAsync(c.Land.Id, new(null, null, false, null, null, false, " "))).Code.ShouldBe("VALIDATION_FAILED");
    }

    [Fact]
    public async Task Factor_tables_are_checked_for_their_rule_kind()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PrimeDbContext>();
        await using var _ = await db.Database.BeginTransactionAsync();
        var seed = await BillingFlowTests.SeedPostedAssessmentAsync(scope.ServiceProvider, db, On);
        var factors = scope.ServiceProvider.GetRequiredService<IAdjustmentFactorService>();
        Task<string?> Message(AdjustmentRuleKind kind, DistanceReference? reference, decimal? depth, params AdjustmentFactorRowRequest[] rows) =>
            factors.CreateAsync(new CreateAdjustmentFactorRequest(seed.SmvId, "L14-BAD", "DEMO", 0m, null, null, "DEMO", On, null, kind, reference, depth, rows))
                .ContinueWith(t => t.Result.Message);

        (await Message(AdjustmentRuleKind.ByDistance, null, null, new AdjustmentFactorRowRequest(null, null, 1m, null, 0m)))!.ShouldContain("distanceReference");
        (await Message(AdjustmentRuleKind.ByDistance, DistanceReference.Poblacion, null, new AdjustmentFactorRowRequest(null, null, 2m, null, 0m), new AdjustmentFactorRowRequest(null, 1m, 3m, null, -5m)))!
            .ShouldContain("overlap");
        (await Message(AdjustmentRuleKind.Depth, null, null, new AdjustmentFactorRowRequest(null, null, null, 1, -20m)))!.ShouldContain("standard depth");
        (await Message(AdjustmentRuleKind.Corner, null, null, new AdjustmentFactorRowRequest(null, null, null, 1, -20m)))!.ShouldContain("no rows");
        (await Message(AdjustmentRuleKind.ByRoadType, null, null))!.ShouldContain("road type");
    }

    [Fact]
    public async Task Market_values_round_to_the_configured_step()
    {
        var host = factory.WithWebHostBuilder(b => b
            .UseSetting("Valuation:MarketValueRoundingStep", "100000")
            .UseSetting("Valuation:MarketValueRoundingLegalBasis", "DEMO — not a rule"));
        var c = await BeginAsync(host);
        await using var _ = c.Scope;
        var valued = (await c.Services.GetRequiredService<IValuationService>().ComputeForLandAsync(c.Land.Id, asOf: On)).Value;
        // Strip 2's 80,000 rounds to the nearest 100,000; strip 1's 500,000 is already round.
        valued.Lines!.Select(l => l.MarketValue).ShouldBe([500_000m, 100_000m]);
        valued.Lines![1].Breakdown.Single(b => b.Key == "MarketValueBeforeRounding").Value.ShouldBe(80_000m);
        valued.ComputedMarketValue.ShouldBe(600_000m);
        host.Dispose();
    }

    [Fact]
    public async Task A_separately_owned_improvement_is_valued_under_its_own_unit()
    {
        var c = await BeginAsync();
        await using var _ = c.Scope;
        var landType = await TestSeed.LandPropertyTypeAsync(c.Db);
        var mango = new ImprovementKind { Code = $"MG{Guid.NewGuid():N}"[..8], Name = "DEMO mango tree" };
        var unit = new RealPropertyUnit
        {
            PropertyId = c.Seed.PropertyId, RpuNumber = $"RPU-OI-{Guid.NewGuid():N}"[..20], RpuType = RpuType.OtherImprovement, EffectivityDate = On,
            LandRpuId = c.Seed.RpuId,
        };
        c.Db.AddRange(mango, unit);
        await c.Db.SaveChangesAsync();
        var smv = c.Services.GetRequiredService<ISmvService>();
        var rate = await smv.CreateScheduleAsync(c.Seed.SmvId, new CreateSmvScheduleRequest(c.Seed.ClassificationId, null, landType.Id, null, "per tree", 2_000m,
            null, null, On, mango.Id));
        rate.IsSuccess.ShouldBeTrue(rate.Message);
        (await TestSeed.AsCheckerAsync(c.Services, () => smv.ApproveScheduleAsync(rate.Value.Id))).IsSuccess.ShouldBeTrue();

        var lands = c.Services.GetRequiredService<ILandService>();
        (await lands.AddImprovementAsync(c.Land.Id, new AddLandImprovementRequest(mango.Id, 10m, true, null, null, null, SeparateRpuId: c.Seed.RpuId)))
            .Code.ShouldBe("SEPARATE_RPU_INVALID"); // the land's own unit is not an other-improvement unit
        var added = await lands.AddImprovementAsync(c.Land.Id, new AddLandImprovementRequest(mango.Id, 10m, true, null, null, null, SeparateRpuId: unit.Id));
        added.IsSuccess.ShouldBeTrue(added.Message);
        added.Value.Improvements.Single().SeparateRpuNumber.ShouldBe(unit.RpuNumber);

        var valuations = c.Services.GetRequiredService<IValuationService>();
        // The land no longer carries the trees …
        (await valuations.ComputeForLandAsync(c.Land.Id, asOf: On)).Value.ComputedMarketValue.ShouldBe(580_000m);
        // … their own unit does.
        var trees = await valuations.ComputeForRpuAsync(unit.Id, asOf: On);
        trees.IsSuccess.ShouldBeTrue(trees.Message);
        (trees.Value.RpuId, trees.Value.ComputedMarketValue, trees.Value.Lines!.Single().Unit).ShouldBe((unit.Id, 20_000m, "per tree"));
    }
}
