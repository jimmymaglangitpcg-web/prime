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
using Prime.Domain.Entities.Transactions;
using Prime.Domain.Enums;
using Prime.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Prime.IntegrationTests;

/// <summary>
/// Step L1-5 (docs/analysis/valuation-foundation.md §4.5): a building valued on the SMV's
/// construction cost, extra-item costs and depreciation table, and when its depreciation may
/// change. Every cost, rate and level is DEMO test data, not an SMV's.
/// </summary>
public class BuildingCostValuationTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private sealed record Ctx(PrimeDbContext Db, IServiceProvider Services, Guid BuildingId, Guid RpuId, Guid SmvId, Guid StructureId,
        Guid FenceTypeId, Guid ClassificationId, Guid UseId)
    {
        public IValuationService Valuation => Services.GetRequiredService<IValuationService>();
        public IBuildingCostTableService Tables => Services.GetRequiredService<IBuildingCostTableService>();
        public IBuildingService Buildings => Services.GetRequiredService<IBuildingService>();
    }

    private static readonly DateOnly Jan2026 = new(2026, 1, 1);

    /// <summary>
    /// A 100 sqm DEMO building completed in 2016, with 20 m of fence, on the seeded property. DEMO
    /// tables under the seeded SMV: BUCC 10,000/sqm; fence 1,500 per linear m; depreciation 2 % a
    /// year for years 1–5 and 3 % a year from year 6, at least 20 % remaining. Level 50 %.
    /// </summary>
    private async Task<(Ctx Ctx, IAsyncDisposable Transaction)> BeginAsync(bool withTables = true)
    {
        var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PrimeDbContext>();
        var transaction = await db.Database.BeginTransactionAsync();
        var seed = await BillingFlowTests.SeedPostedAssessmentAsync(scope.ServiceProvider, db, Jan2026);
        var buildingType = await TestSeed.PropertyTypeAsync(db, "BUILDING", "DEMO_Building");
        var use = new ActualUse { Code = $"AU{Guid.NewGuid():N}"[..8], Name = "DEMO_Dwelling" };
        var kind = new BuildingType { Code = $"BT{Guid.NewGuid():N}"[..8], Name = "DEMO_House" };
        var structure = new StructuralType { Code = $"ST{Guid.NewGuid():N}"[..8], Name = "DEMO_Concrete" };
        var condition = new Condition { Code = $"CO{Guid.NewGuid():N}"[..8], Name = "DEMO_Good" };
        var fence = new BuildingComponentType { Code = $"CT{Guid.NewGuid():N}"[..8], Name = "DEMO_Fence" };
        var rpu = new RealPropertyUnit { PropertyId = seed.PropertyId, RpuNumber = $"RPU-{Guid.NewGuid():N}", RpuType = RpuType.Building, EffectivityDate = Jan2026 };
        db.AddRange(use, kind, structure, condition, fence, rpu);
        db.AssessmentLevels.Add(new AssessmentLevel
        {
            OrdinanceNumber = $"ORD-{Guid.NewGuid():N}"[..20], ClassificationId = seed.ClassificationId, ActualUseId = use.Id,
            PropertyTypeId = buildingType.Id, LowerValue = 0m, AssessmentPercentage = 50m, EffectiveDate = Jan2026,
            Status = WorkflowStatus.Approved, ApprovedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();

        var buildings = scope.ServiceProvider.GetRequiredService<IBuildingService>();
        var building = (await buildings.CreateAsync(new CreateBuildingRequest(rpu.Id, kind.Id, structure.Id, use.Id, 1, 100m, 100m, 2015, 2016, condition.Id, null))).Value;
        (await buildings.AddUsePortionAsync(building.Id, new AddBuildingUsePortionRequest(seed.ClassificationId, use.Id, 100m))).IsSuccess.ShouldBeTrue();
        var item = await buildings.AddComponentAsync(building.Id, new AddBuildingComponentRequest(fence.Id, "DEMO front fence", 20m, null, null, true, null));
        item.IsSuccess.ShouldBeTrue(item.IsSuccess ? null : item.Message);

        var c = new Ctx(db, scope.ServiceProvider, building.Id, rpu.Id, seed.SmvId, structure.Id, fence.Id, seed.ClassificationId, use.Id);
        if (withTables)
        {
            await ApprovedAsync(c.Tables.CreateBuildingCostAsync(new CreateBuildingCostRequest(seed.SmvId, structure.Id, kind.Id, null, 10_000m, "DEMO", Jan2026, null)),
                x => c.Tables.ApproveBuildingCostAsync(x.Id));
            await ApprovedAsync(c.Tables.CreateExtraItemCostAsync(new CreateExtraItemCostRequest(seed.SmvId, fence.Id, "linear m", 1_500m, "DEMO", Jan2026, null)),
                x => c.Tables.ApproveExtraItemCostAsync(x.Id));
            await ApprovedAsync(c.Tables.CreateDepreciationScheduleAsync(new CreateDepreciationScheduleRequest(seed.SmvId, structure.Id,
                DepreciationReading.YearlyWithinBand, 20m, [new(1, 5, 2m), new(6, null, 3m)], "DEMO", Jan2026, null)),
                x => c.Tables.ApproveDepreciationScheduleAsync(x.Id));
        }
        return (c, new Scoped(transaction, scope));
    }

    private static async Task ApprovedAsync<T>(Task<Prime.Application.Common.Result<T>> created, Func<T, Task<Prime.Application.Common.Result<T>>> approve)
    {
        var result = await created;
        result.IsSuccess.ShouldBeTrue(result.IsSuccess ? null : result.Message);
        var approved = await approve(result.Value);
        approved.IsSuccess.ShouldBeTrue(approved.IsSuccess ? null : approved.Message);
    }

    private static async Task<TransactionType> TypeAsync(PrimeDbContext db, bool allowsNewDepreciation)
    {
        var type = new TransactionType
        {
            Code = $"T{Guid.NewGuid():N}"[..12], Name = "DEMO transaction", Kind = PropertyTransactionKind.NewAssessment, LegalBasis = "DEMO",
            EffectiveDate = new DateOnly(2020, 1, 1), Status = WorkflowStatus.Approved, ApprovedAt = DateTimeOffset.UtcNow,
            AllowsNewDepreciation = allowsNewDepreciation,
        };
        db.TransactionTypes.Add(type);
        await db.SaveChangesAsync();
        return type;
    }

    private static async Task PostAsync(Ctx c, Guid valuationId, DateOnly effective)
    {
        var created = await c.Services.GetRequiredService<IAssessmentService>().CreateAsync(new CreateAssessmentRequest(valuationId, effective.Year, effective, null, null, "DEMO"));
        created.IsSuccess.ShouldBeTrue(created.IsSuccess ? null : created.Message);
        var assessment = await c.Db.Assessments.SingleAsync(x => x.Id == created.Value.Id);
        assessment.Status = WorkflowStatus.Posted; // the approve/post workflow is covered by AssessmentFlowTests
        assessment.PostedAt = DateTimeOffset.UtcNow;
        await c.Db.SaveChangesAsync();
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
    public async Task DepreciatedBuilding_GivesTheFaasFigures_AndIsAssessed()
    {
        var (c, tx) = await BeginAsync();
        await using var _ = tx;

        var valuation = await c.Valuation.ComputeForRpuAsync(c.RpuId, asOf: Jan2026);

        valuation.IsSuccess.ShouldBeTrue(valuation.IsSuccess ? null : valuation.Message);
        var b = valuation.Value.Lines.Single().B();
        b["FloorArea"].ShouldBe(100m);
        b["Rate"].ShouldBe(10_000m);              // BUCC
        b["BaseValue"].ShouldBe(1_000_000m);      // core
        b["AdditionalItemsCost"].ShouldBe(30_000m); // 20 m × 1,500
        b["TotalConstructionCost"].ShouldBe(1_030_000m);
        b["Age"].ShouldBe(10m);                   // 2026 − 2016
        b["DepreciationPercent"].ShouldBe(25m);   // 5 × 2 % + 5 × 3 %
        b["DepreciationCarriedOver"].ShouldBe(0m);
        b["Depreciation"].ShouldBe(257_500m);
        b["MarketValue"].ShouldBe(772_500m);
        valuation.Value.ComputedMarketValue.ShouldBe(772_500m);
        // Read in FAAS order: the extra item just before the items' total, the market value last.
        var keys = valuation.Value.Lines.Single().Breakdown.Select(x => x.Key).ToList();
        keys[keys.IndexOf("AdditionalItemsCost") - 1].ShouldStartWith("ExtraItem:");
        keys[^1].ShouldBe("MarketValue");
        valuation.Value.SmvId.ShouldBe(c.SmvId);
        var building = await c.Db.Buildings.AsNoTracking().SingleAsync(x => x.Id == c.BuildingId);
        (building.Depreciation, building.DepreciatedValue).ShouldBe((25m, 772_500m));

        var assessment = await c.Services.GetRequiredService<IAssessmentService>().CreateAsync(new CreateAssessmentRequest(valuation.Value.Id, 2026, Jan2026, null, null, "DEMO"));
        assessment.IsSuccess.ShouldBeTrue(assessment.IsSuccess ? null : assessment.Message);
        assessment.Value.AssessedValue.ShouldBe(386_250m);
    }

    [Fact]
    public async Task Depreciation_IsCarriedOver_UnlessTheTransactionOrARevisionAllowsANewOne()
    {
        var (c, tx) = await BeginAsync();
        await using var _ = tx;
        var first = await c.Valuation.ComputeForRpuAsync(c.RpuId, asOf: Jan2026);
        await PostAsync(c, first.Value.Id, Jan2026);
        var later = new DateOnly(2028, 1, 1);

        // A transfer or correction keeps the posted 25 %.
        var kept = await c.Valuation.ComputeForRpuAsync(c.RpuId, asOf: later, transactionTypeId: (await TypeAsync(c.Db, false)).Id);
        kept.IsSuccess.ShouldBeTrue(kept.IsSuccess ? null : kept.Message);
        kept.Value.Lines.Single().B()["DepreciationPercent"].ShouldBe(25m);
        kept.Value.Lines.Single().B()["DepreciationCarriedOver"].ShouldBe(1m);
        kept.Value.Lines.Single().B().ContainsKey("Age").ShouldBeFalse();
        (await c.Valuation.ComputeForRpuAsync(c.RpuId, asOf: later)).Value.Lines.Single().B()["DepreciationPercent"].ShouldBe(25m);

        // A transaction allowing it, or a general revision: age 12 → 5 × 2 % + 7 × 3 % = 31 %.
        var renewed = await c.Valuation.ComputeForRpuAsync(c.RpuId, asOf: later, transactionTypeId: (await TypeAsync(c.Db, true)).Id);
        renewed.Value.Lines.Single().B()["DepreciationPercent"].ShouldBe(31m);
        renewed.Value.Lines.Single().B()["DepreciationCarriedOver"].ShouldBe(0m);
        (await c.Valuation.ComputeForRpuAsync(c.RpuId, asOf: later, generalRevision: true)).Value.Lines.Single().B()["DepreciationPercent"].ShouldBe(31m);
    }

    [Fact]
    public async Task AssessingUnderAnotherTransaction_IsRefused()
    {
        var (c, tx) = await BeginAsync();
        await using var _ = tx;
        var type = await TypeAsync(c.Db, true);
        var valuation = await c.Valuation.ComputeForRpuAsync(c.RpuId, asOf: Jan2026, transactionTypeId: type.Id);
        var assessments = c.Services.GetRequiredService<IAssessmentService>();

        (await assessments.CreateAsync(new CreateAssessmentRequest(valuation.Value.Id, 2026, Jan2026, null, null, "DEMO")))
            .Code.ShouldBe("VALUATION_TRANSACTION_MISMATCH");
        var same = await assessments.CreateAsync(new CreateAssessmentRequest(valuation.Value.Id, 2026, Jan2026, null, null, "DEMO", TransactionTypeId: type.Id));
        same.IsSuccess.ShouldBeTrue(same.IsSuccess ? null : same.Message);
    }

    [Fact]
    public async Task WhatTheSmvDoesNotGive_StopsTheValuation()
    {
        var (c, tx) = await BeginAsync();
        await using var _ = tx;

        // An extra item without an SMV cost.
        var gate = new BuildingComponentType { Code = $"CT{Guid.NewGuid():N}"[..8], Name = "DEMO_Gate" };
        c.Db.Add(gate);
        await c.Db.SaveChangesAsync();
        (await c.Buildings.AddComponentAsync(c.BuildingId, new AddBuildingComponentRequest(gate.Id, null, 1m, null, null, true, null))).IsSuccess.ShouldBeTrue();
        (await c.Valuation.ComputeForRpuAsync(c.RpuId, asOf: Jan2026)).Code.ShouldBe("EXTRA_ITEM_COST_NOT_FOUND");
        var gateItem = await c.Db.BuildingComponents.SingleAsync(x => x.ComponentTypeId == gate.Id);
        c.Db.BuildingComponents.Remove(gateItem);

        // No age recorded.
        var building = await c.Db.Buildings.SingleAsync(x => x.Id == c.BuildingId);
        (building.YearCompleted, building.YearConstructed) = (null, null);
        await c.Db.SaveChangesAsync();
        (await c.Valuation.ComputeForRpuAsync(c.RpuId, asOf: Jan2026)).Code.ShouldBe("BUILDING_AGE_UNKNOWN");

        // A structural type the SMV has no cost for.
        var timber = new StructuralType { Code = $"ST{Guid.NewGuid():N}"[..8], Name = "DEMO_Timber" };
        c.Db.Add(timber);
        building.StructuralType = timber; // the navigation is tracked from the earlier valuation
        building.YearCompleted = 2016;
        await c.Db.SaveChangesAsync();
        var noCost = await c.Valuation.ComputeForRpuAsync(c.RpuId, asOf: Jan2026);
        noCost.Code.ShouldBe("BUILDING_COST_NOT_FOUND", noCost.IsSuccess ? $"{noCost.Value.Lines[0].Description} {noCost.Value.ComputedMarketValue}" : noCost.Message);

        // A cost but no depreciation table for it.
        await ApprovedAsync(c.Tables.CreateBuildingCostAsync(new CreateBuildingCostRequest(c.SmvId, timber.Id, null, null, 6_000m, "DEMO", Jan2026, null)),
            x => c.Tables.ApproveBuildingCostAsync(x.Id));
        (await c.Valuation.ComputeForRpuAsync(c.RpuId, asOf: Jan2026)).Code.ShouldBe("DEPRECIATION_TABLE_NOT_FOUND");

        // A table that stops before the building's age.
        await ApprovedAsync(c.Tables.CreateDepreciationScheduleAsync(new CreateDepreciationScheduleRequest(c.SmvId, timber.Id,
            DepreciationReading.Cumulative, 20m, [new(0, 5, 10m)], "DEMO", Jan2026, null)), x => c.Tables.ApproveDepreciationScheduleAsync(x.Id));
        (await c.Valuation.ComputeForRpuAsync(c.RpuId, asOf: Jan2026)).Code.ShouldBe("DEPRECIATION_NOT_APPLICABLE");
    }

    [Fact]
    public async Task CumulativeTable_IsCappedByTheRemainingValue()
    {
        var (c, tx) = await BeginAsync(withTables: false);
        await using var _ = tx;
        await ApprovedAsync(c.Tables.CreateBuildingCostAsync(new CreateBuildingCostRequest(c.SmvId, c.StructureId, null, c.ClassificationId, 8_000m, "DEMO", Jan2026, null)),
            x => c.Tables.ApproveBuildingCostAsync(x.Id));
        await ApprovedAsync(c.Tables.CreateExtraItemCostAsync(new CreateExtraItemCostRequest(c.SmvId, c.FenceTypeId, "linear m", 1_000m, "DEMO", Jan2026, null)),
            x => c.Tables.ApproveExtraItemCostAsync(x.Id));
        await ApprovedAsync(c.Tables.CreateDepreciationScheduleAsync(new CreateDepreciationScheduleRequest(c.SmvId, c.StructureId,
            DepreciationReading.Cumulative, 30m, [new(0, 4, 0m), new(5, 9, 50m), new(10, null, 90m)], "DEMO", Jan2026, null)),
            x => c.Tables.ApproveDepreciationScheduleAsync(x.Id));

        var valuation = await c.Valuation.ComputeForRpuAsync(c.RpuId, asOf: Jan2026);

        valuation.IsSuccess.ShouldBeTrue(valuation.IsSuccess ? null : valuation.Message);
        var b = valuation.Value.Lines.Single().B();
        b["TotalConstructionCost"].ShouldBe(820_000m); // 100 × 8,000 (the classification's row) + 20 × 1,000
        b["DepreciationPercent"].ShouldBe(70m);        // the band says 90 %; at least 30 % remains
        b["DepreciationCapped"].ShouldBe(1m);
        b["MarketValue"].ShouldBe(246_000m);
    }

    [Fact]
    public async Task Tables_CheckTheirShape_AndNeedASecondUser()
    {
        var (c, tx) = await BeginAsync(withTables: false);
        await using var _ = tx;

        (await c.Tables.CreateDepreciationScheduleAsync(new CreateDepreciationScheduleRequest(c.SmvId, c.StructureId, DepreciationReading.Cumulative, 20m,
            [new(0, 5, 10m), new(7, null, 20m)], "DEMO", Jan2026, null))).Code.ShouldBe("VALIDATION_FAILED"); // gap 6
        (await c.Tables.CreateDepreciationScheduleAsync(new CreateDepreciationScheduleRequest(c.SmvId, c.StructureId, DepreciationReading.Cumulative, 20m,
            [new(0, null, 10m), new(5, 9, 20m)], "DEMO", Jan2026, null))).Code.ShouldBe("VALIDATION_FAILED"); // open band not last
        (await c.Tables.CreateBuildingCostAsync(new CreateBuildingCostRequest(c.SmvId, c.StructureId, null, null, 0m, "DEMO", Jan2026, null)))
            .Code.ShouldBe("VALIDATION_FAILED");

        var user = c.Services.GetRequiredService<Prime.Infrastructure.Identity.CurrentUserService>();
        var users = Enumerable.Range(0, 2).Select(i => new Prime.Domain.Entities.Identity.AppUser
        {
            SupabaseUserId = Guid.NewGuid(), DisplayName = $"DEMO User {(char)('A' + i)}", Email = $"demo-{Guid.NewGuid():N}@example.invalid",
        }).ToList();
        c.Db.AppUsers.AddRange(users);
        await c.Db.SaveChangesAsync();
        user.AppUserId = users[0].Id;
        var cost = (await c.Tables.CreateBuildingCostAsync(new CreateBuildingCostRequest(c.SmvId, c.StructureId, null, null, 5_000m, "DEMO", Jan2026, null))).Value;
        (await c.Tables.ApproveBuildingCostAsync(cost.Id)).Code.ShouldBe("CANNOT_APPROVE_OWN_BUILDING_COST");
        user.AppUserId = users[1].Id;
        (await c.Tables.ApproveBuildingCostAsync(cost.Id)).Value.Status.ShouldBe(WorkflowStatus.Approved);
    }
}

internal static class ValuationLineBreakdown
{
    /// <summary>A line's breakdown by key.</summary>
    public static Dictionary<string, decimal> B(this ValuationLineDto line) => line.Breakdown.ToDictionary(x => x.Key, x => x.Value);
}
