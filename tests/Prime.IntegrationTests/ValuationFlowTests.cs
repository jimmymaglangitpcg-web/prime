using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
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
/// Phase 5 exit criteria (docs/DEVELOPMENT-ROADMAP.md): given demo SMV data,
/// the valuation engine produces a market value with a stored, reproducible
/// breakdown, and an SMV schedule is never overwritten in place. Same
/// single-scope, rolled-back-transaction pattern as
/// Persistence/ConstraintTests.cs — Application services resolved from the
/// same DI scope share the one PrimeDbContext instance (and its open
/// transaction), per Prime.Infrastructure.DependencyInjection's explicit
/// <c>IApplicationDbContext -&gt; PrimeDbContext</c> forwarding — so calling
/// ISmvService/IValuationService here still rolls back cleanly.
/// </summary>
public class ValuationFlowTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
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
            // Never committed — the real dev database is never polluted by test runs.
            await transaction.DisposeAsync();
            scope.Dispose();
        }
    }

    private static async Task<(Land Land, Guid PropertyTypeId)> SeedLandAsync(PrimeDbContext db, decimal area, decimal? locationFactor = null)
    {
        var province = new Province { PsgcCode = $"P{Guid.NewGuid():N}"[..10], Name = "Demo Province" };
        var municipality = new Municipality { Province = province, PsgcCode = $"M{Guid.NewGuid():N}"[..10], Name = "Demo Municipality" };
        var barangay = new Barangay { Municipality = municipality, PsgcCode = $"B{Guid.NewGuid():N}"[..10], Name = "Demo Barangay" };
        var classification = new Classification { Code = $"CL{Guid.NewGuid():N}"[..8], Name = "DEMO_Residential" };
        var actualUse = new ActualUse { Code = $"AU{Guid.NewGuid():N}"[..8], Name = "DEMO_Residential Use" };
        var propertyType = new PropertyType { Code = "LAND", Name = "DEMO_Land" };
        db.AddRange(province, municipality, barangay, classification, actualUse, propertyType);

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
            RpuType = RpuType.Land,
            EffectivityDate = DateOnly.FromDateTime(DateTime.UtcNow),
        };
        db.RealPropertyUnits.Add(rpu);
        await db.SaveChangesAsync();

        var land = new Land
        {
            Rpu = rpu,
            Property = property,
            Area = area,
            LocationFactor = locationFactor,
            Classification = classification,
            ActualUse = actualUse,
        };
        db.Lands.Add(land);
        await db.SaveChangesAsync();

        return (land, propertyType.Id);
    }

    [Fact]
    public async Task ComputeForLand_WithApprovedSmvSchedule_ProducesReproducibleBreakdown()
    {
        var (db, services, transaction) = await BeginTestScopeAsync(factory);
        await using var _ = transaction;

        var (land, _) = await SeedLandAsync(db, area: 500m, locationFactor: 1.1m);

        var smvService = services.GetRequiredService<ISmvService>();
        var smv = (await smvService.CreateSmvAsync(new CreateSmvRequest(
            OrdinanceNumber: $"ORD-{Guid.NewGuid():N}",
            OrdinanceDate: new DateOnly(2026, 1, 1),
            ApprovalDate: new DateOnly(2026, 1, 15),
            EffectivityDate: new DateOnly(2026, 1, 1),
            RevisionYear: 2026,
            Description: "DEMO_SMV for ValuationFlowTests"))).Value;
        await smvService.ApproveSmvAsync(smv.Id);

        var schedule = (await smvService.CreateScheduleAsync(smv.Id, new CreateSmvScheduleRequest(
            ClassificationId: land.ClassificationId,
            ActualUseId: land.ActualUseId,
            PropertyTypeId: (await db.PropertyTypes.SingleAsync(x => x.Code == "LAND")).Id,
            ZoneId: null,
            Unit: "per sqm",
            MarketValue: 1000m,
            MinimumValue: null,
            MaximumValue: null,
            EffectiveDate: new DateOnly(2026, 1, 1)))).Value;
        await smvService.ApproveScheduleAsync(schedule.Id);

        var valuationService = services.GetRequiredService<IValuationService>();
        var result = await valuationService.ComputeForLandAsync(land.Id);

        result.IsSuccess.ShouldBeTrue(result.IsSuccess ? null : result.Message);
        result.Value.ComputedMarketValue.ShouldBe(550_000m); // 500 sqm * 1000/sqm * 1.1 location factor
        result.Value.SmvScheduleId.ShouldBe(schedule.Id);
        result.Value.Breakdown["Area"].ShouldBe(500m);
        result.Value.Breakdown["Rate"].ShouldBe(1000m);
        result.Value.Breakdown["LocationFactor"].ShouldBe(1.1m);

        // Reload independently — proves the breakdown is actually persisted
        // and reconstructable, not just returned in-memory.
        db.ChangeTracker.Clear();
        var stored = await db.Valuations.SingleAsync(x => x.Id == result.Value.Id);
        stored.ComputedMarketValue.ShouldBe(550_000m);
        var reconstructedBreakdown = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, decimal>>(stored.BreakdownJson);
        reconstructedBreakdown.ShouldNotBeNull();
        reconstructedBreakdown!["Area"].ShouldBe(500m);

        var reloadedLand = await db.Lands.SingleAsync(x => x.Id == land.Id);
        reloadedLand.MarketValue.ShouldBe(550_000m);
    }

    [Fact]
    public async Task ComputeForLand_NoMatchingApprovedSchedule_FailsWithSpecificCode()
    {
        var (db, services, transaction) = await BeginTestScopeAsync(factory);
        await using var _ = transaction;

        var (land, _) = await SeedLandAsync(db, area: 100m);

        var valuationService = services.GetRequiredService<IValuationService>();
        var result = await valuationService.ComputeForLandAsync(land.Id);

        result.IsSuccess.ShouldBeFalse();
        result.Code.ShouldBe("SMV_SCHEDULE_NOT_FOUND");
    }

    [Fact]
    public async Task CreateSecondSchedule_ClosesThePreviousOne_NeverOverwritesInPlace()
    {
        var (db, services, transaction) = await BeginTestScopeAsync(factory);
        await using var _ = transaction;

        var (land, propertyTypeId) = await SeedLandAsync(db, area: 100m);
        var smvService = services.GetRequiredService<ISmvService>();

        var smv = (await smvService.CreateSmvAsync(new CreateSmvRequest(
            OrdinanceNumber: $"ORD-{Guid.NewGuid():N}",
            OrdinanceDate: new DateOnly(2025, 1, 1),
            ApprovalDate: null,
            EffectivityDate: new DateOnly(2025, 1, 1),
            RevisionYear: 2025,
            Description: "DEMO_SMV for supersession test"))).Value;

        var first = (await smvService.CreateScheduleAsync(smv.Id, new CreateSmvScheduleRequest(
            land.ClassificationId, land.ActualUseId, propertyTypeId, null, "per sqm", 800m, null, null,
            EffectiveDate: new DateOnly(2025, 1, 1)))).Value;

        var secondResult = await smvService.CreateScheduleAsync(smv.Id, new CreateSmvScheduleRequest(
            land.ClassificationId, land.ActualUseId, propertyTypeId, null, "per sqm", 950m, null, null,
            EffectiveDate: new DateOnly(2026, 1, 1)));

        secondResult.IsSuccess.ShouldBeTrue(secondResult.IsSuccess ? null : secondResult.Message);

        db.ChangeTracker.Clear();
        var reloadedFirst = await db.SmvSchedules.SingleAsync(x => x.Id == first.Id);
        reloadedFirst.EndDate.ShouldBe(new DateOnly(2025, 12, 31));
        reloadedFirst.MarketValue.ShouldBe(800m); // untouched — closed, not edited

        // Both rows still exist — the old one was never deleted or overwritten.
        (await db.SmvSchedules.CountAsync(x => x.SmvId == smv.Id)).ShouldBe(2);
    }
}
