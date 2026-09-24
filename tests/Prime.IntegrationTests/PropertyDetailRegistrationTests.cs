using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Prime.Application.Features.Buildings;
using Prime.Application.Features.Lands;
using Prime.Application.Features.MachineryUnits;
using Prime.Application.Features.Valuation;
using Prime.Domain.Entities;
using Prime.Domain.Entities.Reference;
using Prime.Domain.Enums;
using Prime.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Prime.IntegrationTests;

/// <summary>
/// Closes the gap flagged in Phase 6's roadmap status: Land/Building/
/// Machinery had no create/update Application service or controller at
/// all before this — only direct EF construction in tests (as
/// ValuationFlowTests/AssessmentFlowTests/GeneralRevisionFlowTests still do
/// for their own setup, which is a legitimate, separate concern from
/// whether a real registration API exists). Same rolled-back-transaction
/// pattern as the other integration test files.
/// </summary>
public class PropertyDetailRegistrationTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private static async Task<(PrimeDbContext Db, IServiceProvider Services, IAsyncDisposable Transaction)> BeginTestScopeAsync(WebApplicationFactory<Program> factory)
    {
        var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PrimeDbContext>();
        var transaction = await db.Database.BeginTransactionAsync();
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

    private static async Task<Guid> SeedRpuAsync(PrimeDbContext db, RpuType rpuType)
    {
        var province = new Province { PsgcCode = $"P{Guid.NewGuid():N}"[..10], Name = "Demo Province" };
        var municipality = new Municipality { Province = province, PsgcCode = $"M{Guid.NewGuid():N}"[..10], Name = "Demo Municipality" };
        var barangay = new Barangay { Municipality = municipality, PsgcCode = $"B{Guid.NewGuid():N}"[..10], Name = "Demo Barangay" };
        db.AddRange(province, municipality, barangay);

        var property = new PropertyEntity
        {
            PropertyIdentificationNumber = $"PIN-{Guid.NewGuid():N}",
            Province = province,
            Municipality = municipality,
            Barangay = barangay,
        };
        db.Properties.Add(property);

        var rpu = new RealPropertyUnit
        {
            Property = property,
            RpuNumber = $"RPU-{Guid.NewGuid():N}",
            RpuType = rpuType,
            EffectivityDate = DateOnly.FromDateTime(DateTime.UtcNow),
        };
        db.RealPropertyUnits.Add(rpu);
        await db.SaveChangesAsync();

        return rpu.Id;
    }

    [Fact]
    public async Task CreateLand_ThenDuplicateForSameRpu_IsRejected()
    {
        var (db, services, transaction) = await BeginTestScopeAsync(factory);
        await using var _ = transaction;

        var classification = new Classification { Code = $"CL{Guid.NewGuid():N}"[..8], Name = "DEMO_Residential" };
        var actualUse = new ActualUse { Code = $"AU{Guid.NewGuid():N}"[..8], Name = "DEMO_Residential Use" };
        db.AddRange(classification, actualUse);
        await db.SaveChangesAsync();

        var rpuId = await SeedRpuAsync(db, RpuType.Land);
        var landService = services.GetRequiredService<ILandService>();

        var created = await landService.CreateAsync(new CreateLandRequest(
            rpuId, 500m, null, classification.Id, actualUse.Id, null, null, 1.1m, null, null, false, null));

        created.IsSuccess.ShouldBeTrue(created.IsSuccess ? null : created.Message);
        created.Value.Area.ShouldBe(500m);
        created.Value.AreaUnit.ShouldBe("sqm"); // default applied
        created.Value.ClassificationName.ShouldBe("DEMO_Residential");

        var fetched = await landService.GetByRpuAsync(rpuId);
        fetched.IsSuccess.ShouldBeTrue();
        fetched.Value.Id.ShouldBe(created.Value.Id);

        var duplicate = await landService.CreateAsync(new CreateLandRequest(
            rpuId, 300m, null, classification.Id, actualUse.Id, null, null, null, null, null, false, null));

        duplicate.IsSuccess.ShouldBeFalse();
        duplicate.Code.ShouldBe("LAND_ALREADY_EXISTS_FOR_RPU");
    }

    [Fact]
    public async Task CreateLand_ForNonLandRpu_IsRejected()
    {
        var (db, services, transaction) = await BeginTestScopeAsync(factory);
        await using var _ = transaction;

        var classification = new Classification { Code = $"CL{Guid.NewGuid():N}"[..8], Name = "DEMO_Residential" };
        var actualUse = new ActualUse { Code = $"AU{Guid.NewGuid():N}"[..8], Name = "DEMO_Residential Use" };
        db.AddRange(classification, actualUse);
        await db.SaveChangesAsync();

        var rpuId = await SeedRpuAsync(db, RpuType.Building); // wrong type on purpose

        var landService = services.GetRequiredService<ILandService>();
        var result = await landService.CreateAsync(new CreateLandRequest(
            rpuId, 100m, null, classification.Id, actualUse.Id, null, null, null, null, null, false, null));

        result.IsSuccess.ShouldBeFalse();
        result.Code.ShouldBe("RPU_TYPE_MISMATCH");
    }

    [Fact]
    public async Task CreateBuilding_ComputesDefaultsAndRoundTrips()
    {
        var (db, services, transaction) = await BeginTestScopeAsync(factory);
        await using var _ = transaction;

        var buildingType = new BuildingType { Code = $"BT{Guid.NewGuid():N}"[..8], Name = "DEMO_Residential Building" };
        var structuralType = new StructuralType { Code = $"ST{Guid.NewGuid():N}"[..8], Name = "DEMO_Concrete" };
        var actualUse = new ActualUse { Code = $"AU{Guid.NewGuid():N}"[..8], Name = "DEMO_Residential Use" };
        var condition = new Condition { Code = $"CO{Guid.NewGuid():N}"[..8], Name = "DEMO_Good" };
        db.AddRange(buildingType, structuralType, actualUse, condition);
        await db.SaveChangesAsync();

        var rpuId = await SeedRpuAsync(db, RpuType.Building);
        var buildingService = services.GetRequiredService<IBuildingService>();

        var result = await buildingService.CreateAsync(new CreateBuildingRequest(
            rpuId, buildingType.Id, structuralType.Id, actualUse.Id, null, 100m, 200m, 2020, 2021, condition.Id, null));

        result.IsSuccess.ShouldBeTrue(result.IsSuccess ? null : result.Message);
        result.Value.NumberOfStoreys.ShouldBe(1); // default applied
        result.Value.CompletionPercentage.ShouldBe(100m); // default applied
        result.Value.TotalFloorArea.ShouldBe(200m);

        var fetched = await buildingService.GetByIdAsync(result.Value.Id);
        fetched.IsSuccess.ShouldBeTrue();
        fetched.Value.StructuralTypeName.ShouldBe("DEMO_Concrete");
    }

    [Fact]
    public async Task CreateMachinery_ComputesAndRoundTrips()
    {
        var (db, services, transaction) = await BeginTestScopeAsync(factory);
        await using var _ = transaction;

        var machineryType = new MachineryType { Code = $"MT{Guid.NewGuid():N}"[..8], Name = "DEMO_Generator" };
        db.MachineryTypes.Add(machineryType);
        await db.SaveChangesAsync();

        var rpuId = await SeedRpuAsync(db, RpuType.Machinery);
        var machineryService = services.GetRequiredService<IMachineryService>();

        var result = await machineryService.CreateAsync(new CreateMachineryRequest(
            rpuId, machineryType.Id, "Backup generator", "DEMO_Brand", "DEMO_Model", "SN-001",
            null, null, new DateOnly(2020, 1, 1), 500_000m, 20_000m, 5_000m, 10, 6));

        result.IsSuccess.ShouldBeTrue(result.IsSuccess ? null : result.Message);
        result.Value.AcquisitionCost.ShouldBe(500_000m);
        result.Value.MachineryTypeName.ShouldBe("DEMO_Generator");

        var fetched = await machineryService.GetByRpuAsync(rpuId);
        fetched.IsSuccess.ShouldBeTrue();
        fetched.Value.SerialNumber.ShouldBe("SN-001");
    }

    private static async Task<(IMachineryService Machinery, IValuationService Valuation, Guid RpuId, Guid MachineryTypeId)> SeedMachineryRpuAsync(
        PrimeDbContext db, IServiceProvider services)
    {
        var machineryType = new MachineryType { Code = $"MT{Guid.NewGuid():N}"[..8], Name = "DEMO_Generator" };
        db.MachineryTypes.Add(machineryType);
        await db.SaveChangesAsync();
        var rpuId = await SeedRpuAsync(db, RpuType.Machinery);
        return (services.GetRequiredService<IMachineryService>(), services.GetRequiredService<IValuationService>(), rpuId, machineryType.Id);
    }

    [Fact]
    public async Task ValueUsedMachinery_WithoutReplacementCost_FailsInsteadOfUsingAcquisitionCost()
    {
        var (db, services, transaction) = await BeginTestScopeAsync(factory);
        await using var _ = transaction;
        var (machinery, valuation, rpuId, typeId) = await SeedMachineryRpuAsync(db, services);

        var created = await machinery.CreateAsync(new CreateMachineryRequest(
            rpuId, typeId, null, null, null, null, null, null, null, 500_000m, null, null, 10, 6));
        created.IsSuccess.ShouldBeTrue(created.IsSuccess ? null : created.Message);

        var result = await valuation.ComputeForMachineryAsync(created.Value.Id);

        result.IsSuccess.ShouldBeFalse();
        result.Code.ShouldBe("MACHINERY_VALUATION_INPUTS_MISSING");
        result.Message.ShouldNotBeNull().ShouldContain("replacement or reproduction cost");
    }

    [Fact]
    public async Task ValueUsedMachinery_AppliesSection225FloorFromConfiguration_AndStoresBreakdown()
    {
        var (db, services, transaction) = await BeginTestScopeAsync(factory);
        await using var _ = transaction;
        var (machinery, valuation, rpuId, typeId) = await SeedMachineryRpuAsync(db, services);

        var created = await machinery.CreateAsync(new CreateMachineryRequest(
            rpuId, typeId, null, null, null, null, null, null, null, 500_000m, null, null, 10, 1,
            IsBrandNew: false, ReplacementCost: 800_000m));
        created.IsSuccess.ShouldBeTrue(created.IsSuccess ? null : created.Message);

        var result = await valuation.ComputeForMachineryAsync(created.Value.Id);

        result.IsSuccess.ShouldBeTrue(result.IsSuccess ? null : result.Message);
        // 800,000 × 1/10 = 80,000, below the configured 20% floor (160,000).
        result.Value.ComputedMarketValue.ShouldBe(160_000m);
        result.Value.ValuationMethod.ShouldBe(ValuationMethod.ReplacementCost);
        result.Value.Breakdown["MinimumApplied"].ShouldBe(1m);
        (await db.MachineryUnits.SingleAsync(x => x.Id == created.Value.Id)).MarketValue.ShouldBe(160_000m);
    }

    [Fact]
    public async Task ValueBrandNewMachinery_IsAcquisitionCost_AndRejectsReplacementCost()
    {
        var (db, services, transaction) = await BeginTestScopeAsync(factory);
        await using var _ = transaction;
        var (machinery, valuation, rpuId, typeId) = await SeedMachineryRpuAsync(db, services);

        var rejected = await machinery.CreateAsync(new CreateMachineryRequest(
            rpuId, typeId, null, null, null, null, null, null, null, 500_000m, 20_000m, null, null, null,
            IsBrandNew: true, ReplacementCost: 1m));
        rejected.IsSuccess.ShouldBeFalse();

        var created = await machinery.CreateAsync(new CreateMachineryRequest(
            rpuId, typeId, null, null, null, null, null, null, null, 500_000m, 20_000m, null, null, null,
            IsBrandNew: true));
        created.IsSuccess.ShouldBeTrue(created.IsSuccess ? null : created.Message);

        var result = await valuation.ComputeForMachineryAsync(created.Value.Id);

        result.IsSuccess.ShouldBeTrue(result.IsSuccess ? null : result.Message);
        result.Value.ComputedMarketValue.ShouldBe(520_000m);
        result.Value.ValuationMethod.ShouldBe(ValuationMethod.AcquisitionCost);
    }
}
