using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Prime.Application.Features.Appraisal;
using Prime.Application.Features.Assessments;
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
/// docs/analysis/mrpaao-forms-model.md §8 step 2d — several machines on one
/// machinery FAAS (MRPAAO Att. 3). Every cost and level is DEMO test data.
/// </summary>
public class MachineryAppraisalTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private sealed record Ctx(PrimeDbContext Db, IServiceProvider Services, Guid RpuId, Guid ClassificationId, Guid SecondUseId, Guid TypeId)
    {
        public IMachineryService Machinery => Services.GetRequiredService<IMachineryService>();
    }

    /// <summary>A machinery RPU whose TD declares the seeded use; DEMO machinery levels: seeded use 80%, second use 50%.</summary>
    private async Task<(Ctx Ctx, IAsyncDisposable Transaction)> BeginAsync()
    {
        var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PrimeDbContext>();
        var transaction = await db.Database.BeginTransactionAsync();
        var seed = await BillingFlowTests.SeedPostedAssessmentAsync(scope.ServiceProvider, db, new DateOnly(2026, 1, 1));
        var machineryType = await TestSeed.PropertyTypeAsync(db, "MACHINERY", "DEMO_Machinery");
        var secondUse = new ActualUse { Code = $"AU{Guid.NewGuid():N}"[..8], Name = "DEMO_Processing" };
        var kind = new MachineryType { Code = $"MT{Guid.NewGuid():N}"[..8], Name = "DEMO_Generator" };
        var rpu = new RealPropertyUnit { PropertyId = seed.PropertyId, RpuNumber = $"RPU-{Guid.NewGuid():N}", RpuType = RpuType.Machinery, EffectivityDate = new DateOnly(2026, 1, 1) };
        db.AddRange(secondUse, kind, rpu, new TaxDeclaration
        {
            Rpu = rpu, PropertyId = seed.PropertyId, TaxDeclarationNumber = $"TD-{Guid.NewGuid():N}", EffectivityDate = new DateOnly(2026, 1, 1),
            Taxability = Taxability.Taxable, ClassificationId = seed.ClassificationId, ActualUseId = seed.TaxDeclaration.ActualUseId,
            AssessmentYear = 2026, Status = WorkflowStatus.Approved,
        });
        foreach (var (use, percent) in new[] { (seed.TaxDeclaration.ActualUseId, 80m), (secondUse.Id, 50m) })
        {
            db.AssessmentLevels.Add(new AssessmentLevel
            {
                OrdinanceNumber = $"ORD-{Guid.NewGuid():N}"[..20], ClassificationId = seed.ClassificationId, ActualUseId = use,
                PropertyTypeId = machineryType.Id, LowerValue = 0m, AssessmentPercentage = percent, EffectiveDate = new DateOnly(2026, 1, 1),
                Status = WorkflowStatus.Approved, ApprovedAt = DateTimeOffset.UtcNow,
            });
        }
        await db.SaveChangesAsync();
        return (new Ctx(db, scope.ServiceProvider, rpu.Id, seed.ClassificationId, secondUse.Id, kind.Id), new Scoped(transaction, scope));
    }

    private sealed class Scoped(IDbContextTransaction transaction, IServiceScope scope) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            await transaction.DisposeAsync();
            scope.Dispose();
        }
    }

    private static CreateMachineryRequest BrandNew(Ctx c, string brand, decimal cost, decimal install, Guid? use = null) =>
        new(c.RpuId, c.TypeId, "DEMO", brand, "M1", $"SN-{Guid.NewGuid():N}"[..10], null, null, new DateOnly(2026, 1, 1), cost, install, null,
            null, null, IsBrandNew: true, ClassificationId: use is null ? null : c.ClassificationId, ActualUseId: use);

    [Fact]
    public async Task TwoMachines_OneFaas_OneLineEach_AssessedUnderTheirOwnUse()
    {
        var (c, tx) = await BeginAsync();
        await using var _ = tx;
        (await c.Machinery.CreateAsync(BrandNew(c, "DEMO_A", 100_000m, 10_000m))).IsSuccess.ShouldBeTrue();
        var second = await c.Machinery.CreateAsync(BrandNew(c, "DEMO_B", 50_000m, 0m, c.SecondUseId));
        second.IsSuccess.ShouldBeTrue(second.IsSuccess ? null : second.Message); // a second machine on the same RPU is allowed
        (await c.Machinery.ListByRpuAsync(c.RpuId)).Value.Count.ShouldBe(2);

        // Valuing either machine values the whole unit.
        var valuation = await c.Services.GetRequiredService<IValuationService>().ComputeForMachineryAsync(second.Value.Id);

        valuation.IsSuccess.ShouldBeTrue(valuation.IsSuccess ? null : valuation.Message);
        valuation.Value.ComputedMarketValue.ShouldBe(160_000m);
        var lines = await c.Db.ValuationLines.Where(x => x.ValuationId == valuation.Value.Id).OrderBy(x => x.Sequence).ToListAsync();
        lines.Select(l => (l.Source, l.MarketValue)).ShouldBe([(ValuationLineSource.Machinery, 110_000m), (ValuationLineSource.Machinery, 50_000m)]);

        var assessment = await c.Services.GetRequiredService<IAssessmentService>().CreateAsync(
            new CreateAssessmentRequest(valuation.Value.Id, 2026, new DateOnly(2026, 1, 1), null, null, "DEMO"));
        assessment.IsSuccess.ShouldBeTrue(assessment.IsSuccess ? null : assessment.Message);
        assessment.Value.Lines.Select(l => (l.ActualUseName, l.AssessedValue)).ShouldBe([("DEMO_Residential Use", 88_000m), ("DEMO_Processing", 25_000m)]);

        var record = (await c.Services.GetRequiredService<IAppraisalRecordService>().GetAsync(assessment.Value.Id)).Value;
        record.MachineryUnits.Select(m => m.Brand).ShouldBe(["DEMO_A", "DEMO_B"]);
    }

    [Fact]
    public async Task AMachineMissingItsInputs_StopsTheUnitsValuation_Naming_It()
    {
        var (c, tx) = await BeginAsync();
        await using var _ = tx;
        var first = (await c.Machinery.CreateAsync(BrandNew(c, "DEMO_A", 100_000m, 0m))).Value;
        (await c.Machinery.CreateAsync(new CreateMachineryRequest(c.RpuId, c.TypeId, "DEMO", "DEMO_Old", "M2", "SN-OLD", null, null,
            new DateOnly(2010, 1, 1), 80_000m, null, null, null, null))).IsSuccess.ShouldBeTrue();

        var valuation = await c.Services.GetRequiredService<IValuationService>().ComputeForMachineryAsync(first.Id);

        valuation.Code.ShouldBe("MACHINERY_VALUATION_INPUTS_MISSING");
        valuation.Message.ShouldNotBeNull().ShouldContain("DEMO_Old");
    }
}
