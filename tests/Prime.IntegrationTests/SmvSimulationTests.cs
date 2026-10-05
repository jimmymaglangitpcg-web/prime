using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Prime.Application.Features.Forms;
using Prime.Application.Features.Smv;
using Prime.Application.Features.SmvSimulations;
using Prime.Application.Features.Valuation;
using Prime.Domain.Entities;
using Prime.Domain.Entities.Identity;
using Prime.Domain.Entities.MarketData;
using Prime.Domain.Enums;
using Prime.Infrastructure.Identity;
using Prime.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Prime.IntegrationTests;

/// <summary>
/// Step L6-3 (docs/analysis/smv-preparation-general-revision.md §4.3): the engine's rate source and compute-only mode, one
/// unit simulated under a proposed (draft) SMV, and a simulation run (its runner called directly, so no real background job
/// is queued). The seed's land is 500 sqm at an approved 1,000/sqm, assessed at 20%; the proposed SMV prices it at 1,500.
/// DEMO data. Rolled back.
/// </summary>
public class SmvSimulationTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly DateOnly Proposed = new(2027, 1, 1);

    private sealed record Ctx(PrimeDbContext Db, IServiceProvider Services, AppUser User, BillingFlowTests.Seed Seed, Guid MunicipalityId, Guid ProposedSmvId)
    {
        public ISmvSimulationService Simulations => Services.GetRequiredService<ISmvSimulationService>();
        public IValuationService Valuation => Services.GetRequiredService<IValuationService>();
    }

    private async Task<(Ctx Ctx, IAsyncDisposable Transaction)> BeginAsync()
    {
        var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PrimeDbContext>();
        var transaction = await db.Database.BeginTransactionAsync();
        try
        {
            var current = scope.ServiceProvider.GetRequiredService<CurrentUserService>();
            current.AppUserId = null;
            var seed = await BillingFlowTests.SeedPostedAssessmentAsync(scope.ServiceProvider, db, new DateOnly(2026, 1, 1));
            var user = new AppUser { SupabaseUserId = Guid.NewGuid(), DisplayName = "DEMO User", Email = $"demo-{Guid.NewGuid():N}@example.invalid" };
            db.AppUsers.Add(user);
            await db.SaveChangesAsync();
            current.AppUserId = user.Id;
            var municipalityId = await db.Properties.Where(x => x.Id == seed.PropertyId).Select(x => x.MunicipalityId).SingleAsync();

            // The proposed SMV and its row stay drafts: never approved.
            var smvs = scope.ServiceProvider.GetRequiredService<ISmvService>();
            var proposed = (await smvs.CreateSmvAsync(new CreateSmvRequest($"DEMO-PROPOSED-{Guid.NewGuid():N}"[..30], new DateOnly(2026, 12, 1), null, Proposed, 2027,
                "DEMO proposed SMV for SmvSimulationTests"))).Value;
            var schedule = await db.SmvSchedules.AsNoTracking().SingleAsync(x => x.SmvId == seed.SmvId);
            (await smvs.CreateScheduleAsync(proposed.Id, new CreateSmvScheduleRequest(
                schedule.ClassificationId, schedule.ActualUseId, schedule.PropertyTypeId, null, "per sqm", 1_500m, null, null, Proposed))).IsSuccess.ShouldBeTrue();
            return (new Ctx(db, scope.ServiceProvider, user, seed, municipalityId, proposed.Id), new Scoped(transaction, scope));
        }
        catch
        {
            await transaction.DisposeAsync();
            scope.Dispose();
            throw;
        }
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
    public async Task ComputeOnly_ValuesWithoutStoring_AndAProposedSmvIsNeverStored()
    {
        var (c, tx) = await BeginAsync();
        await using var _ = tx;
        var stored = await c.Db.Valuations.CountAsync(x => x.RpuId == c.Seed.RpuId);
        var landValue = await c.Db.Lands.Where(x => x.RpuId == c.Seed.RpuId).Select(x => x.MarketValue).SingleAsync();

        var current = await c.Valuation.ComputeForRpuAsync(c.Seed.RpuId, asOf: Proposed, mode: new ValuationMode(ComputeOnly: true));
        current.IsSuccess.ShouldBeTrue(current.Message);
        (current.Value.Id, current.Value.ComputedMarketValue, current.Value.SmvId).ShouldBe((Guid.Empty, 500_000m, (Guid?)c.Seed.SmvId));

        var proposed = await c.Valuation.ComputeForRpuAsync(c.Seed.RpuId, asOf: Proposed, mode: ValuationMode.Proposed(c.ProposedSmvId));
        proposed.IsSuccess.ShouldBeTrue(proposed.Message);
        (proposed.Value.Id, proposed.Value.ComputedMarketValue, proposed.Value.SmvId).ShouldBe((Guid.Empty, 750_000m, (Guid?)c.ProposedSmvId));
        proposed.Value.Lines!.Single().UnitValue.ShouldBe(1_500m);
        proposed.Value.Lines!.Single().ClassificationName.ShouldBe("DEMO_Residential");

        (await c.Valuation.ComputeForRpuAsync(c.Seed.RpuId, asOf: Proposed, mode: new ValuationMode(c.ProposedSmvId)))
            .Code.ShouldBe("PROPOSED_SMV_COMPUTE_ONLY");
        // Before the proposed rows take effect there is no rate under that SMV.
        (await c.Valuation.ComputeForRpuAsync(c.Seed.RpuId, asOf: new DateOnly(2026, 6, 1), mode: ValuationMode.Proposed(c.ProposedSmvId)))
            .Code.ShouldBe("SMV_SCHEDULE_NOT_FOUND");

        await c.Db.SaveChangesAsync();
        c.Db.ChangeTracker.Clear();
        (await c.Db.Valuations.CountAsync(x => x.RpuId == c.Seed.RpuId)).ShouldBe(stored);
        (await c.Db.Lands.Where(x => x.RpuId == c.Seed.RpuId).Select(x => x.MarketValue).SingleAsync()).ShouldBe(landValue);
    }

    [Fact]
    public async Task SimulateUnit_ValuesAndAssessesUnderTheProposedSmv()
    {
        var (c, tx) = await BeginAsync();
        await using var _ = tx;
        var unit = await c.Simulations.SimulateUnitAsync(c.Seed.RpuId, c.ProposedSmvId, Proposed);
        unit.IsSuccess.ShouldBeTrue(unit.Message);
        (unit.Value.MarketValue, unit.Value.AssessedValue, unit.Value.TaxableAssessedValue, unit.Value.Failure)
            .ShouldBe(((decimal?)750_000m, (decimal?)150_000m, (decimal?)150_000m, (string?)null));
        var line = unit.Value.Lines.Single();
        (line.ClassificationName, line.AssessmentPercentage, line.Taxability).ShouldBe(("DEMO_Residential", (decimal?)20m, Taxability.Taxable));
        (await c.Db.Assessments.CountAsync(x => x.RpuId == c.Seed.RpuId)).ShouldBe(1);

        (await c.Simulations.SimulateUnitAsync(Guid.NewGuid(), c.ProposedSmvId, Proposed)).Code.ShouldBe("RPU_NOT_FOUND");
        (await c.Simulations.SimulateUnitAsync(c.Seed.RpuId, Guid.NewGuid(), Proposed)).Code.ShouldBe("PROPOSED_SMV_NOT_FOUND");
    }

    [Fact]
    public async Task ARun_RecordsCurrentAndSimulatedValues_AndAFailureWithItsReason_WithoutStoringValuations()
    {
        var (c, tx) = await BeginAsync();
        await using var _ = tx;
        // A second unit in scope that cannot be valued: a land unit without its land description.
        var bare = new RealPropertyUnit { PropertyId = c.Seed.PropertyId, RpuNumber = $"RPU-{Guid.NewGuid():N}", RpuType = RpuType.Land, EffectivityDate = new DateOnly(2024, 1, 1) };
        c.Db.Add(bare);
        c.Db.Add(new TaxDeclaration
        {
            Rpu = bare, PropertyId = c.Seed.PropertyId, TaxDeclarationNumber = $"TD-{Guid.NewGuid():N}", EffectivityDate = new DateOnly(2024, 1, 1),
            Taxability = Taxability.Taxable, ClassificationId = c.Seed.ClassificationId, ActualUseId = c.Seed.TaxDeclaration.ActualUseId, AssessmentYear = 2024,
            Status = WorkflowStatus.Approved,
        });
        await c.Db.SaveChangesAsync();
        var valuations = await c.Db.Valuations.CountAsync();
        var assessments = await c.Db.Assessments.CountAsync();

        // The run row as StartAsync makes it, without queueing a real job.
        var run = new SmvSimulationRun
        {
            SmvId = c.ProposedSmvId, AsOf = Proposed, StartedBy = c.User.Id, StartedAt = DateTimeOffset.UtcNow,
            Scope = [new SmvSimulationScope { MunicipalityId = c.MunicipalityId }],
        };
        c.Db.SmvSimulationRuns.Add(run);
        await c.Db.SaveChangesAsync();
        await c.Services.GetRequiredService<SmvSimulationRunner>().RunAsync(run.Id, CancellationToken.None);
        c.Db.ChangeTracker.Clear();

        var dto = (await c.Simulations.GetAsync(run.Id)).Value;
        (dto.Status, dto.TotalCount, dto.ProcessedCount, dto.FailedCount).ShouldBe((JobExecutionStatus.Completed, 2, 2, 1));
        var summary = dto.Summary.ShouldNotBeNull();
        (summary.Simulated, summary.Failed, summary.Compared, summary.Higher, summary.Lower, summary.ClassificationChanged).ShouldBe((1, 1, 1, 1, 0, 0));
        (summary.CurrentMarketValue, summary.SimulatedMarketValue, summary.CurrentAssessedValue, summary.SimulatedAssessedValue)
            .ShouldBe((500_000m, 750_000m, 100_000m, 150_000m));

        var results = (await c.Simulations.SearchResultsAsync(run.Id, new SmvSimulationResultSearchRequest())).Value;
        results.TotalCount.ShouldBe(2);
        var land = results.Items.Single(x => x.RpuId == c.Seed.RpuId);
        (land.CurrentAssessedValue, land.SimulatedAssessedValue, land.MarketValueChange, land.MarketValueChangePercent, land.ClassificationChanged)
            .ShouldBe(((decimal?)100_000m, (decimal?)150_000m, (decimal?)250_000m, (decimal?)50m, false));
        (land.CurrentClassification, land.SimulatedClassification).ShouldBe(("DEMO_Residential", "DEMO_Residential"));
        var failed = (await c.Simulations.SearchResultsAsync(run.Id, new SmvSimulationResultSearchRequest(Failed: true))).Value.Items.Single();
        (failed.RpuId, failed.SimulatedMarketValue).ShouldBe((bare.Id, (decimal?)null));
        failed.FailureReason.ShouldNotBeNullOrWhiteSpace();

        (await c.Db.Valuations.CountAsync()).ShouldBe(valuations);
        (await c.Db.Assessments.CountAsync()).ShouldBe(assessments);
    }

    [Fact]
    public async Task Start_IsRefused_ForARejectedSmv_AnEmptyScope_OrAMunicipalityTheSmvDoesNotCover()
    {
        var (c, tx) = await BeginAsync();
        await using var _ = tx;
        (await c.Simulations.StartAsync(new(c.ProposedSmvId, Proposed, [], null))).Code.ShouldBe("VALIDATION_FAILED");
        (await c.Simulations.StartAsync(new(Guid.NewGuid(), Proposed, [c.MunicipalityId], null))).Code.ShouldBe("SMV_NOT_FOUND");

        var other = new Prime.Domain.Entities.Reference.Municipality
        {
            ProvinceId = await c.Db.Municipalities.Where(x => x.Id == c.MunicipalityId).Select(x => x.ProvinceId).SingleAsync(),
            PsgcCode = $"M{Guid.NewGuid():N}"[..10], Name = "DEMO Other Municipality",
        };
        c.Db.Add(other);
        c.Db.Add(new SmvCoverage { SmvId = c.ProposedSmvId, MunicipalityId = c.MunicipalityId });
        await c.Db.SaveChangesAsync();
        (await c.Simulations.StartAsync(new(c.ProposedSmvId, Proposed, [other.Id], null))).Code.ShouldBe("SMV_NOT_APPLICABLE");

        await c.Db.Smvs.Where(x => x.Id == c.ProposedSmvId).ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, WorkflowStatus.Rejected));
        (await c.Simulations.StartAsync(new(c.ProposedSmvId, Proposed, [c.MunicipalityId], null))).Code.ShouldBe("SMV_NOT_FOUND");
    }

    private static MarketTransaction Sale(Ctx c, decimal area, decimal consideration, AreaMeasure unit = AreaMeasure.SquareMetre,
        MarketDataReview review = MarketDataReview.Accepted, bool classified = true, bool withBuilding = false) => new()
    {
        Source = MarketDataSource.RegistryAbstract, TransactionDate = new DateOnly(2026, 6, 1), MunicipalityId = c.MunicipalityId,
        ClassificationId = classified ? c.Seed.ClassificationId : null, ActualUseId = c.Seed.TaxDeclaration.ActualUseId,
        LandArea = area, LandAreaUnit = unit, Consideration = consideration, ConveysBuilding = withBuilding, BuildingFloorArea = withBuilding ? 50m : null,
        Review = review, ReviewedAt = review == MarketDataReview.Unreviewed ? null : DateTimeOffset.UtcNow,
    };

    [Fact]
    public async Task ValuationTest_RatiosOfAcceptedLandSales_MedianAndDispersion_AndTheReport()
    {
        var (c, tx) = await BeginAsync();
        await using var _ = tx;
        // Under the proposed 1,500/sqm: ratios 1.0, 0.8, 1.2 and 1.25 (0.05 ha = 500 sqm); median 1.1, CoD 0.1625/1.1 = 14.77%.
        c.Db.MarketTransactions.AddRange(
            Sale(c, 500m, 750_000m), Sale(c, 200m, 375_000m), Sale(c, 100m, 125_000m), Sale(c, 0.05m, 600_000m, AreaMeasure.Hectare),
            Sale(c, 300m, 450_000m, classified: false), Sale(c, 300m, 900_000m, withBuilding: true),
            Sale(c, 300m, 1m, review: MarketDataReview.Unreviewed));
        await c.Db.SaveChangesAsync();
        var tests = c.Services.GetRequiredService<IValuationTestService>();

        var created = await tests.CreateAsync(new(c.ProposedSmvId, Proposed, [c.MunicipalityId], null, null, "DEMO test"));
        created.IsSuccess.ShouldBeTrue(created.Message);
        (created.Value.SalesCount, created.Value.TestedCount).ShouldBe((6, 4));
        var all = created.Value.Groups!.Single(g => g.Level == "All");
        (all.Count, all.MedianRatio, all.CoefficientOfDispersion, all.MedianWithinBenchmark, all.DispersionWithinBenchmark)
            .ShouldBe((4, (decimal?)1.1m, (decimal?)14.77m, (bool?)null, (bool?)null));
        created.Value.Groups!.Single(g => g.Level == "Municipality").Name.ShouldBe("Demo Municipality");
        created.Value.Groups!.Single(g => g.Level == "SubClass").Name.ShouldBe("DEMO_Residential (no sub-class)");

        var sales = (await tests.ListSalesAsync(created.Value.Id)).Value;
        var hectare = sales.Single(s => s.LandAreaUnit == AreaMeasure.Hectare);
        (hectare.UnitValue, hectare.Value, hectare.Ratio).ShouldBe(((decimal?)1_500m, (decimal?)750_000m, (decimal?)1.25m));
        sales.Count(s => s.ExclusionReason != null).ShouldBe(2);
        sales.Single(s => s.ClassificationName == null).ExclusionReason.ShouldBe("The sale records no classification.");

        var preview = await c.Services.GetRequiredService<IFormService>().PreviewAsync("VALUATION_TEST_REPORT", created.Value.Id);
        preview.IsSuccess.ShouldBeTrue(preview.Message);
        preview.Value.Html.ShouldContain("VALUATION TESTING REPORT");
        preview.Value.Html.ShouldContain("14.77");
        preview.Value.Html.ShouldContain("none configured");

        (await tests.CreateAsync(new(c.ProposedSmvId, Proposed, [c.MunicipalityId], new DateOnly(2026, 7, 1), new DateOnly(2026, 1, 1), null)))
            .Code.ShouldBe("VALIDATION_FAILED");
    }

    [Theory]
    [InlineData("per sqm", 2, AreaMeasure.Hectare, 20_000)]
    [InlineData("per hectare", 5_000, AreaMeasure.SquareMetre, 0.5)]
    [InlineData("per ha", 3, AreaMeasure.Hectare, 3)]
    [InlineData("per sq. m.", 120, AreaMeasure.SquareMetre, 120)]
    public void AreaIn_ConvertsBetweenSquareMetresAndHectares(string unit, decimal area, AreaMeasure areaUnit, decimal expected) =>
        ValuationTestService.AreaIn(unit, area, areaUnit).ShouldBe(expected);

    [Fact]
    public void AreaIn_OfAnUnrecognisedUnit_IsNull() => ValuationTestService.AreaIn("per tree", 10m, AreaMeasure.SquareMetre).ShouldBeNull();
}
