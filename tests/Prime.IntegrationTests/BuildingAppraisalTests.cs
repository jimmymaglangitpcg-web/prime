using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Prime.Application.Features.Assessments;
using Prime.Application.Features.Buildings;
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
/// docs/analysis/mrpaao-forms-model.md §8 step 2c — building use portions and
/// additional items (MRPAAO Att. 2). Every rate, cost and level is DEMO test data.
/// </summary>
public class BuildingAppraisalTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private sealed record Ctx(PrimeDbContext Db, IServiceProvider Services, Guid BuildingId, Guid RpuId, Guid ClassificationId,
        Guid FirstUseId, Guid SecondUseId, Guid FenceTypeId)
    {
        public IBuildingService Buildings => Services.GetRequiredService<IBuildingService>();
    }

    /// <summary>
    /// A 100 sqm DEMO building on the seeded property. DEMO rates: 10,000/sqm for the
    /// first use (level 50%), 8,000/sqm for the second (level 30%).
    /// </summary>
    private async Task<(Ctx Ctx, IAsyncDisposable Transaction)> BeginAsync()
    {
        var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PrimeDbContext>();
        var transaction = await db.Database.BeginTransactionAsync();
        var seed = await BillingFlowTests.SeedPostedAssessmentAsync(scope.ServiceProvider, db, new DateOnly(2026, 1, 1));
        var buildingType = await TestSeed.PropertyTypeAsync(db, "BUILDING", "DEMO_Building");
        var firstUse = new ActualUse { Code = $"AU{Guid.NewGuid():N}"[..8], Name = "DEMO_Shop" };
        var secondUse = new ActualUse { Code = $"AU{Guid.NewGuid():N}"[..8], Name = "DEMO_Dwelling" };
        var kind = new BuildingType { Code = $"BT{Guid.NewGuid():N}"[..8], Name = "DEMO_Mixed-use Building" };
        var structure = new StructuralType { Code = $"ST{Guid.NewGuid():N}"[..8], Name = "DEMO_Concrete" };
        var condition = new Condition { Code = $"CO{Guid.NewGuid():N}"[..8], Name = "DEMO_Good" };
        var fence = new BuildingComponentType { Code = $"CT{Guid.NewGuid():N}"[..8], Name = "DEMO_Fence" };
        var rpu = new RealPropertyUnit { PropertyId = seed.PropertyId, RpuNumber = $"RPU-{Guid.NewGuid():N}", RpuType = RpuType.Building, EffectivityDate = new DateOnly(2026, 1, 1) };
        db.AddRange(firstUse, secondUse, kind, structure, condition, fence, rpu);
        foreach (var (use, percent) in new[] { (firstUse, 50m), (secondUse, 30m) })
        {
            db.AssessmentLevels.Add(new AssessmentLevel
            {
                OrdinanceNumber = $"ORD-{Guid.NewGuid():N}"[..20], ClassificationId = seed.ClassificationId, ActualUseId = use.Id,
                PropertyTypeId = buildingType.Id, LowerValue = 0m, AssessmentPercentage = percent, EffectiveDate = new DateOnly(2026, 1, 1),
                Status = WorkflowStatus.Approved, ApprovedAt = DateTimeOffset.UtcNow,
            });
        }
        await db.SaveChangesAsync();

        var smv = scope.ServiceProvider.GetRequiredService<ISmvService>();
        foreach (var (use, rate) in new[] { (firstUse, 10_000m), (secondUse, 8_000m) })
        {
            var schedule = await smv.CreateScheduleAsync(seed.SmvId,
                new CreateSmvScheduleRequest(seed.ClassificationId, use.Id, buildingType.Id, null, "per sqm", rate, null, null, new DateOnly(2026, 1, 1)));
            schedule.IsSuccess.ShouldBeTrue(schedule.IsSuccess ? null : schedule.Message);
            (await smv.ApproveScheduleAsync(schedule.Value.Id)).IsSuccess.ShouldBeTrue();
        }
        var building = await scope.ServiceProvider.GetRequiredService<IBuildingService>().CreateAsync(new CreateBuildingRequest(
            rpu.Id, kind.Id, structure.Id, firstUse.Id, 2, 50m, 100m, 2020, 2021, condition.Id, null));
        building.IsSuccess.ShouldBeTrue(building.IsSuccess ? null : building.Message);
        return (new Ctx(db, scope.ServiceProvider, building.Value.Id, rpu.Id, seed.ClassificationId, firstUse.Id, secondUse.Id, fence.Id),
            new Scoped(transaction, scope));
    }

    private sealed class Scoped(IDbContextTransaction transaction, IServiceScope scope) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            await transaction.DisposeAsync();
            scope.Dispose();
        }
    }

    [Fact]
    public async Task UsePortions_ValueSeparately_WithAdditionalItemsSpreadOrAssigned_AndAssessByUse()
    {
        var (c, tx) = await BeginAsync();
        await using var _ = tx;
        (await c.Buildings.AddUsePortionAsync(c.BuildingId, new AddBuildingUsePortionRequest(c.ClassificationId, c.FirstUseId, 60m))).IsSuccess.ShouldBeTrue();
        var building = (await c.Buildings.AddUsePortionAsync(c.BuildingId, new AddBuildingUsePortionRequest(c.ClassificationId, c.SecondUseId, 40m))).Value;
        var dwelling = building.UsePortions.Single(p => p.ActualUseId == c.SecondUseId);
        // A fence for the whole building (spread 60/40), and a garage for the dwelling only.
        (await c.Buildings.AddComponentAsync(c.BuildingId, new AddBuildingComponentRequest(c.FenceTypeId, "DEMO fence", null, null, 10_000m, true, null))).IsSuccess.ShouldBeTrue();
        var garage = await c.Buildings.AddComponentAsync(c.BuildingId, new AddBuildingComponentRequest(c.FenceTypeId, "DEMO garage", 1m, 5_000m, null, true, dwelling.Id));
        garage.IsSuccess.ShouldBeTrue(garage.IsSuccess ? null : garage.Message);
        garage.Value.Components.Single(x => x.Description == "DEMO garage").Cost.ShouldBe(5_000m); // quantity × unit cost

        var valuation = await c.Services.GetRequiredService<IValuationService>().ComputeForRpuAsync(c.RpuId);

        valuation.IsSuccess.ShouldBeTrue(valuation.IsSuccess ? null : valuation.Message);
        var lines = await c.Db.ValuationLines.Where(x => x.ValuationId == valuation.Value.Id).OrderBy(x => x.Sequence).ToListAsync();
        lines.Select(l => (l.Source, l.MarketValue)).ShouldBe([
            (ValuationLineSource.BuildingUsePortion, 606_000m), // 60 × 10,000 + 6,000 of the fence
            (ValuationLineSource.BuildingUsePortion, 329_000m), // 40 × 8,000 + 4,000 fence + 5,000 garage
        ]);
        valuation.Value.ComputedMarketValue.ShouldBe(935_000m);

        var assessment = await c.Services.GetRequiredService<IAssessmentService>().CreateAsync(
            new CreateAssessmentRequest(valuation.Value.Id, 2026, new DateOnly(2026, 1, 1), null, null, "DEMO"));
        assessment.IsSuccess.ShouldBeTrue(assessment.IsSuccess ? null : assessment.Message);
        assessment.Value.Lines.Select(l => (l.ActualUseName, l.AssessedValue)).ShouldBe([("DEMO_Shop", 303_000m), ("DEMO_Dwelling", 98_700m)]);
        assessment.Value.AssessedValue.ShouldBe(401_700m);
    }

    [Fact]
    public async Task PortionsNotCoveringTheFloorArea_StopTheValuation_AndCannotExceedIt()
    {
        var (c, tx) = await BeginAsync();
        await using var _ = tx;
        (await c.Buildings.AddUsePortionAsync(c.BuildingId, new AddBuildingUsePortionRequest(c.ClassificationId, c.FirstUseId, 60m))).IsSuccess.ShouldBeTrue();

        (await c.Services.GetRequiredService<IValuationService>().ComputeForRpuAsync(c.RpuId)).Code.ShouldBe("BUILDING_USE_PORTIONS_INCOMPLETE");
        (await c.Buildings.AddUsePortionAsync(c.BuildingId, new AddBuildingUsePortionRequest(c.ClassificationId, c.SecondUseId, 41m)))
            .Code.ShouldBe("BUILDING_USE_PORTIONS_EXCEED_FLOOR_AREA");
    }

    [Fact]
    public async Task AdditionalItemWithoutCost_IsRefused()
    {
        var (c, tx) = await BeginAsync();
        await using var _ = tx;

        (await c.Buildings.AddComponentAsync(c.BuildingId, new AddBuildingComponentRequest(c.FenceTypeId, "DEMO", null, null, null, true, null)))
            .Code.ShouldBe("VALIDATION_FAILED");
    }
}
