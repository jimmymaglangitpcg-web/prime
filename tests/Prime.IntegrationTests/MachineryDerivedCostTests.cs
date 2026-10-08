using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Prime.Application.Features.MachineryUnits;
using Prime.Application.Features.Valuation;
using Prime.Domain.Entities;
using Prime.Domain.Entities.Audit;
using Prime.Domain.Entities.Reference;
using Prime.Domain.Enums;
using Prime.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Prime.IntegrationTests;

/// <summary>
/// Step L1-6 (docs/analysis/valuation-foundation.md §4.6): machinery valued from its acquisition cost,
/// the exchange rates and a price index, depreciated at most 5 % a year, with the minimum remaining
/// value only while in operation. Every rate, index and cost is DEMO test data (currency XTS is the
/// ISO 4217 code reserved for testing).
/// </summary>
public class MachineryDerivedCostTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private sealed record Ctx(PrimeDbContext Db, IServiceProvider Services, Guid RpuId, Guid TypeId, string Series)
    {
        public IMachineryService Machinery => Services.GetRequiredService<IMachineryService>();
        public IValuationService Valuation => Services.GetRequiredService<IValuationService>();
        public IMachineryIndexService Indices => Services.GetRequiredService<IMachineryIndexService>();
    }

    private static readonly DateOnly Jan2026 = new(2026, 1, 1);

    /// <summary>A machinery unit with DEMO observations: XTS 50 pesos on 2020-01-01, 60 on 2026-01-01; index 100 for 2020, 110 for 2026.</summary>
    private async Task<(Ctx Ctx, IAsyncDisposable Transaction)> BeginAsync(bool withIndices = true)
    {
        var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PrimeDbContext>();
        var transaction = await db.Database.BeginTransactionAsync();
        var seed = await BillingFlowTests.SeedPostedAssessmentAsync(scope.ServiceProvider, db, Jan2026);
        var kind = new MachineryType { Code = $"MT{Guid.NewGuid():N}"[..8], Name = "DEMO_Mill" };
        var rpu = new RealPropertyUnit { PropertyId = seed.PropertyId, RpuNumber = $"RPU-{Guid.NewGuid():N}", RpuType = RpuType.Machinery, EffectivityDate = Jan2026 };
        db.AddRange(kind, rpu);
        await db.SaveChangesAsync();
        var c = new Ctx(db, scope.ServiceProvider, rpu.Id, kind.Id, $"DEMO-{Guid.NewGuid():N}"[..20]);
        if (withIndices)
        {
            // Rates left in the shared dev DB for these dates would collide; the test's own are what it reads.
            await db.ExchangeRates.Where(x => x.Currency == "XTS").ExecuteDeleteAsync();
            foreach (var (date, rate) in new[] { (new DateOnly(2020, 1, 1), 50m), (Jan2026, 60m) })
            {
                var created = await c.Indices.CreateExchangeRateAsync(new CreateExchangeRateRequest("XTS", date, rate, "DEMO — not a BSP rate", null));
                (await TestSeed.AsCheckerAsync(c.Services, () => c.Indices.ApproveExchangeRateAsync(created.Value.Id))).IsSuccess.ShouldBeTrue();
            }
            foreach (var (year, value) in new[] { (2020, 100m), (2026, 110m) })
            {
                var created = await c.Indices.CreatePriceIndexAsync(new CreatePriceIndexRequest(c.Series, year, value, "DEMO — not a published index", null));
                (await TestSeed.AsCheckerAsync(c.Services, () => c.Indices.ApprovePriceIndexAsync(created.Value.Id))).IsSuccess.ShouldBeTrue();
            }
        }
        return (c, new Scoped(transaction, scope));
    }

    private sealed class Scoped(IDbContextTransaction transaction, IServiceScope scope) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            await transaction.DisposeAsync();
            scope.Dispose();
        }
    }

    /// <summary>
    /// An imported DEMO machine: 1,000,000 pesos at acquisition (2020-01-15), freight 50,000 and insurance
    /// 10,000, installation 40,000; installed 2020-03-01; economic life <paramref name="life"/> years.
    /// </summary>
    private static CreateMachineryRequest Imported(Ctx c, int life, DateOnly? acquired = null, bool inOperation = true) =>
        new(c.RpuId, c.TypeId, "DEMO", "DEMO Mill", "M1", $"SN-{Guid.NewGuid():N}"[..10], null, null, acquired ?? new DateOnly(2020, 1, 15),
            1_000_000m, null, null, life, null,
            IsImported: true, AcquisitionCurrency: "XTS", ForeignAcquisitionCost: 20_000m, OriginCountry: "DEMO country", PriceIndexSeries: c.Series,
            DateInstalled: acquired is null ? new DateOnly(2020, 3, 1) : null, IsInOperation: inOperation,
            CostItems: [new(MachineryCostItemKind.Freight, 50_000m, null), new(MachineryCostItemKind.Insurance, 10_000m, null),
                new(MachineryCostItemKind.Installation, 40_000m, null)]);

    private static Dictionary<string, decimal> Breakdown(ValuationDto v) => v.Lines.Single().Breakdown.ToDictionary(x => x.Key, x => x.Value);

    [Fact]
    public async Task ImportedMachine_IsConvertedTrendedAndDepreciated_AtMostFivePercentAYear()
    {
        var (c, tx) = await BeginAsync();
        await using var _ = tx;
        (await c.Machinery.CreateAsync(Imported(c, life: 10))).IsSuccess.ShouldBeTrue();

        var valuation = await c.Valuation.ComputeForRpuAsync(c.RpuId, asOf: Jan2026);

        valuation.IsSuccess.ShouldBeTrue(valuation.IsSuccess ? null : valuation.Message);
        valuation.Value.ValuationMethod.ShouldBe(ValuationMethod.DerivedReplacementCost);
        var b = Breakdown(valuation.Value);
        b["CostInsuranceFreight"].ShouldBe(1_060_000m);
        (b["ExchangeRateAtAcquisition"], b["ExchangeRateAtValuation"]).ShouldBe((50m, 60m));
        b["PriceIndexFactor"].ShouldBe(1.1m);
        b["OtherExpenses"].ShouldBe(40_000m);
        b["ReplacementCost"].ShouldBe(1_439_200m);  // 1,060,000 × 60/50 × 110/100 + 40,000
        b["YearsInUse"].ShouldBe(5m);               // installed 2020-03-01, valued 2026-01-01
        b["DepreciationPercent"].ShouldBe(25m);     // 5 ÷ 10 = 50 %, capped at 5 % × 5
        b["DepreciationCapped"].ShouldBe(1m);
        b["MarketValue"].ShouldBe(1_079_400m);
        (await c.Db.MachineryUnits.AsNoTracking().SingleAsync(x => x.RpuId == c.RpuId)).Depreciation.ShouldBe(25m);
    }

    [Fact]
    public async Task TheMinimumRemainingValue_HoldsOnlyWhileInOperation()
    {
        var (c, tx) = await BeginAsync();
        await using var _ = tx;
        // Acquired 2008: 18 years of use, capped at 90 %; 10 % would remain, below the 20 % minimum.
        foreach (var year in new[] { 2008, 2026 })
        {
            var created = await c.Indices.CreatePriceIndexAsync(new CreatePriceIndexRequest($"{c.Series}L", year, 100m, "DEMO", null));
            (await TestSeed.AsCheckerAsync(c.Services, () => c.Indices.ApprovePriceIndexAsync(created.Value.Id))).IsSuccess.ShouldBeTrue();
        }
        var request = Imported(c, life: 10, acquired: new DateOnly(2008, 1, 1)) with
        {
            IsImported = false, AcquisitionCurrency = null, ForeignAcquisitionCost = null, PriceIndexSeries = $"{c.Series}L", CostItems = [],
        };
        var machine = (await c.Machinery.CreateAsync(request)).Value;

        var running = Breakdown((await c.Valuation.ComputeForRpuAsync(c.RpuId, asOf: Jan2026)).Value);
        (running["DepreciationPercent"], running["MinimumApplied"], running["MarketValue"]).ShouldBe((90m, 1m, 200_000m));
        running.ContainsKey("ExchangeRateAtAcquisition").ShouldBeFalse(); // local machinery: no conversion

        var stopped = await c.Machinery.UpdateValuationInputsAsync(machine.Id, new UpdateMachineryValuationInputsRequest(
            false, null, null, null, $"{c.Series}L", null, false, [], "DEMO ocular inspection: no longer in operation"));
        stopped.IsSuccess.ShouldBeTrue(stopped.IsSuccess ? null : stopped.Message);
        var idle = Breakdown((await c.Valuation.ComputeForRpuAsync(c.RpuId, asOf: Jan2026)).Value);
        (idle["InOperation"], idle["MinimumApplied"], idle["MarketValue"]).ShouldBe((0m, 0m, 100_000m));
        (await c.Db.Set<AuditLog>().AnyAsync(a => a.RecordId == machine.Id && a.Reason == "DEMO ocular inspection: no longer in operation")).ShouldBeTrue();
    }

    [Fact]
    public async Task MissingObservations_StopTheValuation_AndTheEnteredMethodStays()
    {
        var (c, tx) = await BeginAsync(withIndices: false);
        await using var _ = tx;
        await c.Db.ExchangeRates.Where(x => x.Currency == "XTS").ExecuteDeleteAsync();
        var machine = (await c.Machinery.CreateAsync(Imported(c, life: 10))).Value;

        (await c.Valuation.ComputeForRpuAsync(c.RpuId, asOf: Jan2026)).Code.ShouldBe("PRICE_INDEX_NOT_FOUND");
        foreach (var year in new[] { 2020, 2026 })
        {
            var created = await c.Indices.CreatePriceIndexAsync(new CreatePriceIndexRequest(c.Series, year, 100m, "DEMO", null));
            (await TestSeed.AsCheckerAsync(c.Services, () => c.Indices.ApprovePriceIndexAsync(created.Value.Id))).IsSuccess.ShouldBeTrue();
        }
        (await c.Valuation.ComputeForRpuAsync(c.RpuId, asOf: Jan2026)).Code.ShouldBe("EXCHANGE_RATE_NOT_FOUND");
        var draft = await c.Indices.CreateExchangeRateAsync(new CreateExchangeRateRequest("XTS", new DateOnly(2019, 1, 1), 40m, "DEMO", null));
        (await c.Valuation.ComputeForRpuAsync(c.RpuId, asOf: Jan2026)).Code.ShouldBe("EXCHANGE_RATE_NOT_FOUND"); // a draft is not read
        (await TestSeed.AsCheckerAsync(c.Services, () => c.Indices.ApproveExchangeRateAsync(draft.Value.Id))).IsSuccess.ShouldBeTrue();
        // The latest approved rate on or before each date: 2019's 40 serves both.
        Breakdown((await c.Valuation.ComputeForRpuAsync(c.RpuId, asOf: Jan2026)).Value)["ExchangeRateAtValuation"].ShouldBe(40m);

        // Without a series, the entered replacement cost is the method, as before.
        (await c.Machinery.UpdateValuationInputsAsync(machine.Id, new UpdateMachineryValuationInputsRequest(
            true, "XTS", null, null, null, null, true, [], "DEMO: no index data"))).IsSuccess.ShouldBeTrue();
        (await c.Valuation.ComputeForRpuAsync(c.RpuId, asOf: Jan2026)).Code.ShouldBe("MACHINERY_VALUATION_INPUTS_MISSING");
    }

    [Fact]
    public async Task Observations_CheckTheirShape_NeedASecondUser_AndAreNeverEdited()
    {
        var (c, tx) = await BeginAsync(withIndices: false);
        await using var _ = tx;
        (await c.Indices.CreateExchangeRateAsync(new CreateExchangeRateRequest("usd", Jan2026, 1m, "DEMO", null))).Code.ShouldBe("VALIDATION_FAILED");
        (await c.Indices.CreatePriceIndexAsync(new CreatePriceIndexRequest(c.Series, 2026, 0m, "DEMO", null))).Code.ShouldBe("VALIDATION_FAILED");
        (await c.Machinery.CreateAsync(Imported(c, 10) with { AcquisitionCurrency = null })).Code.ShouldBe("VALIDATION_FAILED");

        var user = c.Services.GetRequiredService<Prime.Infrastructure.Identity.CurrentUserService>();
        var users = Enumerable.Range(0, 2).Select(i => new Prime.Domain.Entities.Identity.AppUser
        {
            SupabaseUserId = Guid.NewGuid(), DisplayName = $"DEMO User {(char)('A' + i)}", Email = $"demo-{Guid.NewGuid():N}@example.invalid",
        }).ToList();
        c.Db.AppUsers.AddRange(users);
        await c.Db.SaveChangesAsync();
        user.AppUserId = users[0].Id;
        var first = (await c.Indices.CreatePriceIndexAsync(new CreatePriceIndexRequest(c.Series, 2026, 100m, "DEMO", null))).Value;
        var second = (await c.Indices.CreatePriceIndexAsync(new CreatePriceIndexRequest(c.Series, 2026, 101m, "DEMO", null))).Value;
        (await c.Indices.ApprovePriceIndexAsync(first.Id)).Code.ShouldBe("CANNOT_APPROVE_OWN_PRICE_INDEX");
        user.AppUserId = users[1].Id;
        (await TestSeed.AsCheckerAsync(c.Services, () => c.Indices.ApprovePriceIndexAsync(first.Id))).IsSuccess.ShouldBeTrue();
        (await TestSeed.AsCheckerAsync(c.Services, () => c.Indices.ApprovePriceIndexAsync(second.Id))).Code.ShouldBe("PRICE_INDEX_EXISTS");
    }
}
