using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Prime.Application.Features.Buildings;
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
/// Step L1-7 (docs/analysis/valuation-foundation.md §4.7): values determined outside the SMV by the market,
/// income or cost approach, with their basis, evidence and named inputs. Every figure is DEMO test data.
/// </summary>
public class IndependentAppraisalTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly DateOnly Jan2026 = new(2026, 1, 1);

    private sealed record Ctx(PrimeDbContext Db, IServiceProvider Services, BillingFlowTests.Seed Seed, Guid LandId)
    {
        public IIndependentAppraisalService Appraisals => Services.GetRequiredService<IIndependentAppraisalService>();
        public IValuationService Valuation => Services.GetRequiredService<IValuationService>();
    }

    /// <summary>The seeded DEMO land: 500 sqm at 1,000/sqm under its SMV.</summary>
    private async Task<(Ctx Ctx, IAsyncDisposable Transaction)> BeginAsync()
    {
        var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PrimeDbContext>();
        var transaction = await db.Database.BeginTransactionAsync();
        var seed = await BillingFlowTests.SeedPostedAssessmentAsync(scope.ServiceProvider, db, Jan2026);
        var landId = await db.Lands.Where(x => x.RpuId == seed.RpuId).Select(x => x.Id).SingleAsync();
        return (new Ctx(db, scope.ServiceProvider, seed, landId), new Scoped(transaction, scope));
    }

    private sealed class Scoped(IDbContextTransaction transaction, IServiceScope scope) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            await transaction.DisposeAsync();
            scope.Dispose();
        }
    }

    private static CreateIndependentAppraisalRequest Request(Guid rpuId, IndependentAppraisalSubject subject, Guid subjectId, decimal value,
        AppraisalApproach approach = AppraisalApproach.Income, IReadOnlyList<IndependentAppraisalInputRequest>? inputs = null) =>
        new(rpuId, subject, subjectId, approach, value, Jan2026, "DEMO basis: income capitalised", "DEMO file IA-0001", inputs);

    private static Dictionary<string, decimal> B(ValuationLineDto line) => line.Breakdown.ToDictionary(x => x.Key, x => x.Value);

    [Fact]
    public async Task AppraisedLand_ReplacesItsSmvValue_KeepsTheInputs_AndWithdrawingRestoresTheSmv()
    {
        var (c, tx) = await BeginAsync();
        await using var _ = tx;
        var appraisal = await c.Appraisals.CreateAsync(Request(c.Seed.RpuId, IndependentAppraisalSubject.Land, c.LandId, 640_000m, inputs:
            [new("Net income", 48_000m, "PHP/yr"), new("Capitalisation rate", 7.5m, "%")]));
        appraisal.IsSuccess.ShouldBeTrue(appraisal.IsSuccess ? null : appraisal.Message);

        var valued = (await c.Valuation.ComputeForRpuAsync(c.Seed.RpuId, asOf: Jan2026)).Value;
        valued.ValuationMethod.ShouldBe(ValuationMethod.IndependentAppraisal);
        valued.ComputedMarketValue.ShouldBe(640_000m);
        var line = valued.Lines.Single();
        line.IndependentAppraisalId.ShouldBe(appraisal.Value.Id);
        line.Description.ShouldBe("Independent appraisal, income approach");
        var b = B(line);
        (b["AppraisedValue"], b["Input:Net income (PHP/yr)"], b["Input:Capitalisation rate (%)"]).ShouldBe((640_000m, 48_000m, 7.5m));
        line.Breakdown.Select(x => x.Key).First().ShouldStartWith("Input:"); // the inputs first, then the value

        // A later appraisal replaces it; a withdrawal (with its reason) brings back the SMV value.
        var second = (await c.Appraisals.CreateAsync(Request(c.Seed.RpuId, IndependentAppraisalSubject.Land, c.LandId, 700_000m, AppraisalApproach.Market))).Value;
        var history = (await c.Appraisals.ListByRpuAsync(c.Seed.RpuId)).Value;
        history.Select(h => (h.Value, h.IsCurrent)).ShouldBe([(700_000m, true), (640_000m, false)]);
        history[1].EndReason.ShouldBe("Replaced by the appraisal of 2026-01-01.");
        (await c.Valuation.ComputeForRpuAsync(c.Seed.RpuId, asOf: Jan2026)).Value.ComputedMarketValue.ShouldBe(700_000m);

        (await c.Appraisals.WithdrawAsync(second.Id, new("DEMO: comparable sales found; the SMV applies"))).IsSuccess.ShouldBeTrue();
        var smv = (await c.Valuation.ComputeForRpuAsync(c.Seed.RpuId, asOf: Jan2026)).Value;
        (smv.ValuationMethod, smv.ComputedMarketValue).ShouldBe((ValuationMethod.SmvBased, 500_000m));
        (await c.Db.Set<AuditLog>().AnyAsync(a => a.RecordId == second.Id && a.Reason == "DEMO: comparable sales found; the SMV applies")).ShouldBeTrue();
        (await c.Appraisals.WithdrawAsync(second.Id, new("again"))).Code.ShouldBe("INDEPENDENT_APPRAISAL_NOT_CURRENT");
    }

    [Fact]
    public async Task AppraisedBuilding_IsSharedOverItsPortionsByFloorArea()
    {
        var (c, tx) = await BeginAsync();
        await using var _ = tx;
        var kind = new BuildingType { Code = $"BT{Guid.NewGuid():N}"[..8], Name = "DEMO_Chapel" };
        var structure = new StructuralType { Code = $"ST{Guid.NewGuid():N}"[..8], Name = "DEMO_Stone" };
        var condition = new Condition { Code = $"CO{Guid.NewGuid():N}"[..8], Name = "DEMO_Fair" };
        var use = new ActualUse { Code = $"AU{Guid.NewGuid():N}"[..8], Name = "DEMO_Worship" };
        var rpu = new RealPropertyUnit { PropertyId = c.Seed.PropertyId, RpuNumber = $"RPU-{Guid.NewGuid():N}", RpuType = RpuType.Building, EffectivityDate = Jan2026 };
        c.Db.AddRange(kind, structure, condition, use, rpu);
        await c.Db.SaveChangesAsync();
        await TestSeed.PropertyTypeAsync(c.Db, "BUILDING", "DEMO_Building");
        var buildings = c.Services.GetRequiredService<IBuildingService>();
        var building = (await buildings.CreateAsync(new CreateBuildingRequest(rpu.Id, kind.Id, structure.Id, use.Id, 1, 100m, 100m, 1900, 1900, condition.Id, null))).Value;
        (await buildings.AddUsePortionAsync(building.Id, new AddBuildingUsePortionRequest(c.Seed.ClassificationId, use.Id, 75m))).IsSuccess.ShouldBeTrue();
        (await buildings.AddUsePortionAsync(building.Id, new AddBuildingUsePortionRequest(c.Seed.ClassificationId, c.Seed.TaxDeclaration.ActualUseId, 25m))).IsSuccess.ShouldBeTrue();
        // No SMV rate covers it: it cannot be valued until appraised.
        (await c.Valuation.ComputeForRpuAsync(rpu.Id, asOf: Jan2026)).IsSuccess.ShouldBeFalse();

        (await c.Appraisals.CreateAsync(Request(rpu.Id, IndependentAppraisalSubject.Building, building.Id, 1_000_000m, AppraisalApproach.Cost))).IsSuccess.ShouldBeTrue();
        var valued = (await c.Valuation.ComputeForRpuAsync(rpu.Id, asOf: Jan2026)).Value;

        valued.Lines.Select(l => (l.Quantity, l.MarketValue)).ShouldBe([(75m, 750_000m), (25m, 250_000m)]);
        B(valued.Lines[0])["Share"].ShouldBe(0.75m);
        valued.Lines.ShouldAllBe(l => l.Description == "Independent appraisal, cost approach");
    }

    [Fact]
    public async Task AppraisedMachine_IsValuedByItsAppraisal_NotItsMissingInputs()
    {
        var (c, tx) = await BeginAsync();
        await using var _ = tx;
        var kind = new MachineryType { Code = $"MT{Guid.NewGuid():N}"[..8], Name = "DEMO_Turbine" };
        var rpu = new RealPropertyUnit { PropertyId = c.Seed.PropertyId, RpuNumber = $"RPU-{Guid.NewGuid():N}", RpuType = RpuType.Machinery, EffectivityDate = Jan2026 };
        c.Db.AddRange(kind, rpu);
        await c.Db.SaveChangesAsync();
        var machine = (await c.Services.GetRequiredService<IMachineryService>().CreateAsync(new CreateMachineryRequest(
            rpu.Id, kind.Id, "DEMO", "DEMO Turbine", "T1", null, null, null, null, 0m, null, null, null, null))).Value;
        (await c.Valuation.ComputeForRpuAsync(rpu.Id, asOf: Jan2026)).Code.ShouldBe("MACHINERY_VALUATION_INPUTS_MISSING");

        (await c.Appraisals.CreateAsync(Request(rpu.Id, IndependentAppraisalSubject.Machinery, machine.Id, 2_500_000m, AppraisalApproach.Market))).IsSuccess.ShouldBeTrue();
        var line = (await c.Valuation.ComputeForRpuAsync(rpu.Id, asOf: Jan2026)).Value.Lines.Single();

        (line.MarketValue, line.Description).ShouldBe((2_500_000m, "DEMO Turbine T1 DEMO — Independent appraisal, market approach"));
    }

    [Fact]
    public async Task TheSubjectMustBeTheUnitsOwn_AndTheBasisAndEvidenceAreRequired()
    {
        var (c, tx) = await BeginAsync();
        await using var _ = tx;
        var other = new RealPropertyUnit { PropertyId = c.Seed.PropertyId, RpuNumber = $"RPU-{Guid.NewGuid():N}", RpuType = RpuType.Building, EffectivityDate = Jan2026 };
        c.Db.Add(other);
        await c.Db.SaveChangesAsync();

        (await c.Appraisals.CreateAsync(Request(other.Id, IndependentAppraisalSubject.Land, c.LandId, 1m))).Code.ShouldBe("APPRAISAL_SUBJECT_INVALID");
        (await c.Appraisals.CreateAsync(Request(c.Seed.RpuId, IndependentAppraisalSubject.Land, c.LandId, 1m) with { Evidence = " " })).Code.ShouldBe("VALIDATION_FAILED");
        (await c.Appraisals.CreateAsync(Request(c.Seed.RpuId, IndependentAppraisalSubject.Land, c.LandId, 1m,
            inputs: [new("Rate", 1m, null), new("rate", 2m, null)]))).Code.ShouldBe("VALIDATION_FAILED");
    }
}
