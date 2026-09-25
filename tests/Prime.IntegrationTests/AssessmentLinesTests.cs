using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Prime.Application.Features.Appraisal;
using Prime.Application.Features.Assessments;
using Prime.Application.Features.Billing.Bills;
using Prime.Domain.Entities;
using Prime.Domain.Entities.Reference;
using Prime.Domain.Enums;
using Prime.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Prime.IntegrationTests;

/// <summary>
/// docs/analysis/mrpaao-forms-model.md §8 step 2a — valuation lines grouped
/// into assessment lines, a level per line, and billing per line. Every
/// rate, level and value is DEMO test data.
/// </summary>
public class AssessmentLinesTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private sealed record Ctx(PrimeDbContext Db, IServiceProvider Services, BillingFlowTests.Seed Seed, Guid LandId, Guid SecondUseId);

    /// <summary>
    /// The seeded land (DEMO_Residential, one use at 20%) plus a second actual
    /// use with two DEMO brackets: up to 250,000 at 5%, above it at 15%.
    /// </summary>
    private async Task<(Ctx Ctx, IAsyncDisposable Transaction)> BeginAsync(WebApplicationFactory<Program>? host = null)
    {
        var scope = (host ?? factory).Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PrimeDbContext>();
        var transaction = await db.Database.BeginTransactionAsync();
        var seed = await BillingFlowTests.SeedPostedAssessmentAsync(scope.ServiceProvider, db, new DateOnly(2026, 1, 1));
        var secondUse = new ActualUse { Code = $"AU{Guid.NewGuid():N}"[..8], Name = "DEMO_Second Use" };
        db.Add(secondUse);
        await db.SaveChangesAsync();
        var landType = await TestSeed.LandPropertyTypeAsync(db);
        // Inserted directly: the level service keeps one open level per classification/use/type,
        // so it cannot hold two brackets side by side (a known gap, docs/analysis/mrpaao-forms-model.md §9).
        foreach (var (lower, upper, percent) in new (decimal, decimal?, decimal)[] { (0m, 250_000m, 5m), (250_000.01m, null, 15m) })
        {
            db.AssessmentLevels.Add(new AssessmentLevel
            {
                OrdinanceNumber = $"ORD-{Guid.NewGuid():N}"[..20], OrdinanceDate = new DateOnly(2026, 1, 1), ClassificationId = seed.ClassificationId,
                ActualUseId = secondUse.Id, PropertyTypeId = landType.Id, LowerValue = lower, UpperValue = upper, AssessmentPercentage = percent,
                EffectiveDate = new DateOnly(2026, 1, 1), Status = WorkflowStatus.Approved, ApprovedAt = DateTimeOffset.UtcNow,
            });
        }
        await db.SaveChangesAsync();
        var landId = await db.Lands.Where(x => x.RpuId == seed.RpuId).Select(x => x.Id).SingleAsync();
        return (new Ctx(db, scope.ServiceProvider, seed, landId, secondUse.Id), new Scoped(transaction, scope));
    }

    private sealed class Scoped(IDbContextTransaction transaction, IServiceScope scope) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            await transaction.DisposeAsync();
            scope.Dispose();
        }
    }

    /// <summary>A two-line land valuation: 300,000 under the seeded use, 200,000 under the second use.</summary>
    private static async Task<Valuation> TwoLineValuationAsync(Ctx c)
    {
        var useId = c.Seed.TaxDeclaration.ActualUseId;
        var valuation = new Valuation
        {
            RpuId = c.Seed.RpuId, PropertyId = c.Seed.PropertyId, SourceType = ValuationSourceType.Land, SourceId = c.LandId,
            ValuationMethod = ValuationMethod.SmvBased, ComputedMarketValue = 500_000m, BreakdownJson = "{\"MarketValue\":500000}",
            EffectiveDate = new DateOnly(2026, 1, 1), ComputedAt = DateTimeOffset.UtcNow,
            Lines =
            [
                new() { Sequence = 1, Source = ValuationLineSource.Land, SourceId = c.LandId, ClassificationId = c.Seed.ClassificationId, ActualUseId = useId, MarketValue = 300_000m },
                new() { Sequence = 2, Source = ValuationLineSource.Land, SourceId = c.LandId, ClassificationId = c.Seed.ClassificationId, ActualUseId = c.SecondUseId, MarketValue = 200_000m },
            ],
        };
        c.Db.Valuations.Add(valuation);
        await c.Db.SaveChangesAsync();
        return valuation;
    }

    private static Task<Application.Common.Result<AssessmentDto>> AssessAsync(Ctx c, Guid valuationId) =>
        c.Services.GetRequiredService<IAssessmentService>().CreateAsync(new CreateAssessmentRequest(valuationId, 2026, new DateOnly(2026, 1, 1), null, null, "DEMO"));

    [Fact]
    public async Task SingleRowValuation_GivesOneLine_AndTheAssessmentKeepsItsLevel()
    {
        var (c, tx) = await BeginAsync();
        await using var _ = tx;

        var a = (await c.Services.GetRequiredService<IAssessmentService>().GetByIdAsync(c.Seed.AssessmentId)).Value;

        var line = a.Lines.ShouldHaveSingleItem();
        line.MarketValue.ShouldBe(a.MarketValue);
        line.AssessedValue.ShouldBe(a.AssessedValue);
        line.AssessmentPercentage.ShouldBe(20m);
        a.AssessmentPercentage.ShouldBe(20m);
        a.AssessmentLevelId.ShouldBe(line.AssessmentLevelId);
        var valuation = await c.Db.Valuations.Include(x => x.Lines).SingleAsync(x => x.Id == a.ValuationId);
        valuation.Lines.ShouldHaveSingleItem().MarketValue.ShouldBe(valuation.ComputedMarketValue);
    }

    [Fact]
    public async Task TwoUses_GiveTwoLines_EachAtItsOwnLevel_WithTheLinesOwnMarketValueAsBracket()
    {
        var (c, tx) = await BeginAsync();
        await using var _ = tx;
        var valuation = await TwoLineValuationAsync(c);

        var result = await AssessAsync(c, valuation.Id);

        result.IsSuccess.ShouldBeTrue(result.IsSuccess ? null : result.Message);
        var a = result.Value;
        a.Lines.Select(l => (l.ActualUseName, l.MarketValue, l.AssessmentPercentage, l.AssessedValue))
            .ShouldBe([("DEMO_Residential Use", 300_000m, 20m, 60_000m), ("DEMO_Second Use", 200_000m, 5m, 10_000m)]);
        a.MarketValue.ShouldBe(500_000m);
        a.AssessedValue.ShouldBe(70_000m);
        a.AssessmentLevelId.ShouldBeNull(); // mixed use: the lines carry the levels
        a.AssessmentPercentage.ShouldBeNull();

        var record = (await c.Services.GetRequiredService<IAppraisalRecordService>().GetAsync(a.Id)).Value;
        record.Assessment.Lines.Count.ShouldBe(2);
        record.Assessment.ActualUse.ShouldBe("DEMO_Residential Use"); // principal: the larger line
        record.Assessment.AssessmentLevelPercent.ShouldBeNull();
        record.Valuation.Lines.Count.ShouldBe(2);
    }

    [Fact]
    public async Task UnitBracketBasis_LooksUpEveryLinesLevelWithTheUnitsTotal()
    {
        await using var unitBasis = factory.WithWebHostBuilder(b => b.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?> { ["Assessment:LevelBracketBasis"] = "Unit" })));
        var (c, tx) = await BeginAsync(unitBasis);
        await using var _ = tx;
        var valuation = await TwoLineValuationAsync(c);

        var a = (await AssessAsync(c, valuation.Id)).Value;

        // 500,000 (the unit) is above 250,000, so the second use takes 15%, not 5%.
        a.Lines.Single(l => l.ActualUseName == "DEMO_Second Use").AssessedValue.ShouldBe(30_000m);
        a.AssessedValue.ShouldBe(90_000m);
    }

    [Fact]
    public async Task LineWithNoLevel_IsRefused_NamingIt()
    {
        var (c, tx) = await BeginAsync();
        await using var _ = tx;
        var orphanUse = new ActualUse { Code = $"AU{Guid.NewGuid():N}"[..8], Name = "DEMO_Unlevelled Use" };
        c.Db.Add(orphanUse);
        var valuation = await TwoLineValuationAsync(c);
        valuation.Lines[1].ActualUseId = orphanUse.Id;
        await c.Db.SaveChangesAsync();

        var result = await AssessAsync(c, valuation.Id);

        result.Code.ShouldBe("ASSESSMENT_LEVEL_NOT_FOUND");
        result.Message.ShouldNotBeNull().ShouldContain("DEMO_Unlevelled Use");
    }

    [Fact]
    public async Task Bill_TaxesEachLineAtItsClassificationsRate()
    {
        var (c, tx) = await BeginAsync();
        await using var _ = tx;
        await BillingFlowTests.RetireExistingRulesAsync(c.Db); // rules left in the shared dev DB (rolled back with the test)
        var (basic, _) = await BillingFlowTests.SeedRulesAsync(c.Db, new DateOnly(2026, 1, 1), promptDiscount: null);
        var valuation = await TwoLineValuationAsync(c);
        var created = (await AssessAsync(c, valuation.Id)).Value;
        var assessment = await c.Db.Assessments.SingleAsync(x => x.Id == created.Id);
        assessment.Status = WorkflowStatus.Posted;
        await c.Db.SaveChangesAsync();

        var bill = await c.Services.GetRequiredService<IBillService>().GenerateAsync(new GenerateBillRequest(c.Seed.RpuId, 2026, new DateOnly(2026, 1, 15)));

        bill.IsSuccess.ShouldBeTrue(bill.IsSuccess ? null : bill.Message);
        bill.Value.AssessmentId.ShouldBe(created.Id);
        bill.Value.AssessedValue.ShouldBe(70_000m);
        var basicTax = bill.Value.TaxTypes.Single(t => t.TaxTypeId == basic.Id);
        basicTax.Lines.Select(l => (l.AssessedValue, l.Tax)).ShouldBe([(60_000m, 600m), (10_000m, 100m)]); // DEMO 1% each
        basicTax.AnnualTax.ShouldBe(700m);
    }
}
