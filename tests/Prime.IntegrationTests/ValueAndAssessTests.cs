using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Prime.Application.Common;
using Prime.Application.Features.AssessmentLevels;
using Prime.Application.Features.Assessments;
using Prime.Application.Features.Smv;
using Prime.Application.Features.Valuation;
using Prime.Domain.Entities;
using Prime.Domain.Enums;
using Prime.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Prime.IntegrationTests;

/// <summary>
/// docs/analysis/value-and-assess.md §2 (VA-1): valuing a whole unit with its rows,
/// assessment preview, the previous-assessment rule, and assessment-level brackets.
/// DEMO rules only.
/// </summary>
public class ValueAndAssessTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    /// <summary>
    /// The seeded land RPU (500 sqm × 1,000 per sqm; DEMO level 0–1,000,000 at 20%, effective 2026-01-01)
    /// with its posted assessment (AV 100,000).
    /// </summary>
    private sealed record Ctx(PrimeDbContext Db, IServiceProvider Services, BillingFlowTests.Seed Seed, Guid ActualUseId, Guid PropertyTypeId)
    {
        public IValuationService Valuations => Services.GetRequiredService<IValuationService>();
        public IAssessmentService Assessments => Services.GetRequiredService<IAssessmentService>();
        public IAssessmentLevelService Levels => Services.GetRequiredService<IAssessmentLevelService>();
    }

    private async Task<(Ctx Ctx, IAsyncDisposable Transaction)> BeginAsync()
    {
        var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PrimeDbContext>();
        var transaction = await db.Database.BeginTransactionAsync();
        var seed = await BillingFlowTests.SeedPostedAssessmentAsync(scope.ServiceProvider, db, new DateOnly(2026, 1, 1));
        var actualUseId = await db.Lands.Where(x => x.RpuId == seed.RpuId).Select(x => x.ActualUseId).SingleAsync();
        var propertyType = await TestSeed.LandPropertyTypeAsync(db);
        return (new Ctx(db, scope.ServiceProvider, seed, actualUseId, propertyType.Id), new Scoped(transaction, scope));
    }

    private sealed class Scoped(IDbContextTransaction transaction, IServiceScope scope) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            await transaction.DisposeAsync();
            scope.Dispose();
        }
    }

    private static CreateAssessmentRequest Request(Guid valuationId, Guid? previous = null) =>
        new(valuationId, 2027, new DateOnly(2027, 1, 1), previous, null, "DEMO reassessment");

    private static async Task<Guid> LevelAsync(Ctx c, decimal lower, decimal? upper, decimal percentage, DateOnly effective)
    {
        var created = await c.Levels.CreateAsync(new CreateAssessmentLevelRequest(
            $"ORD-{Guid.NewGuid():N}", effective, c.Seed.ClassificationId, c.ActualUseId, c.PropertyTypeId, lower, upper, percentage, effective));
        created.IsSuccess.ShouldBeTrue(created.IsSuccess ? null : created.Message);
        (await c.Levels.ApproveAsync(created.Value.Id)).IsSuccess.ShouldBeTrue();
        return created.Value.Id;
    }

    private async Task<decimal?> LevelForAreaAsync(Ctx c, decimal area)
    {
        await c.Db.Lands.Where(x => x.RpuId == c.Seed.RpuId).ExecuteUpdateAsync(x => x.SetProperty(l => l.Area, area));
        c.Db.ChangeTracker.Clear(); // ExecuteUpdate bypasses tracked entities
        var valuation = (await c.Valuations.ComputeForRpuAsync(c.Seed.RpuId)).Value;
        var preview = await c.Assessments.PreviewAsync(Request(valuation.Id));
        preview.IsSuccess.ShouldBeTrue(preview.IsSuccess ? null : preview.Message);
        return preview.Value.Lines.Single().AssessmentPercentage;
    }

    [Fact]
    public async Task ValuingAUnit_ReturnsItsRowsInCalculationOrder_AndTheSmv()
    {
        var (c, tx) = await BeginAsync();
        await using var _ = tx;

        var valuation = await c.Valuations.ComputeForRpuAsync(c.Seed.RpuId);

        valuation.IsSuccess.ShouldBeTrue(valuation.IsSuccess ? null : valuation.Message);
        valuation.Value.ComputedMarketValue.ShouldBe(500_000m);
        valuation.Value.SmvRevisionYear.ShouldBe(2026);
        valuation.Value.SmvOrdinanceNumber.ShouldNotBeNullOrEmpty();
        var line = valuation.Value.Lines.ShouldNotBeNull().Single();
        line.MarketValue.ShouldBe(500_000m);
        line.ClassificationName.ShouldBe("DEMO_Residential");
        line.Breakdown.Last().Key.ShouldBe("MarketValue"); // inputs first, the result last
        (await c.Valuations.ListByRpuAsync(c.Seed.RpuId)).Value.First().Lines.ShouldNotBeNull().Count.ShouldBe(1);
    }

    [Fact]
    public async Task Preview_GivesWhatCreateSaves_WithoutSaving()
    {
        var (c, tx) = await BeginAsync();
        await using var _ = tx;
        var valuation = (await c.Valuations.ComputeForRpuAsync(c.Seed.RpuId)).Value;
        var before = await c.Db.Assessments.CountAsync(x => x.RpuId == c.Seed.RpuId);

        var preview = await c.Assessments.PreviewAsync(Request(valuation.Id, c.Seed.AssessmentId));

        preview.IsSuccess.ShouldBeTrue(preview.IsSuccess ? null : preview.Message);
        preview.Value.AssessedValue.ShouldBe(100_000m);
        preview.Value.Lines.Single().AssessmentPercentage.ShouldBe(20m);
        preview.Value.Lines.Single().ClassificationName.ShouldBe("DEMO_Residential");
        (await c.Db.Assessments.CountAsync(x => x.RpuId == c.Seed.RpuId)).ShouldBe(before);

        var created = (await c.Assessments.CreateAsync(Request(valuation.Id, c.Seed.AssessmentId))).Value;
        created.AssessedValue.ShouldBe(preview.Value.AssessedValue);
        created.Lines.Select(l => (l.MarketValue, l.AssessmentPercentage, l.AssessedValue))
            .ShouldBe(preview.Value.Lines.Select(l => (l.MarketValue, l.AssessmentPercentage, l.AssessedValue)));
        created.PreviousAssessmentId.ShouldBe(c.Seed.AssessmentId);
    }

    [Fact]
    public async Task Posting_SaysWhetherATaxDeclarationWasPrepared()
    {
        var (c, tx) = await BeginAsync();
        await using var _ = tx;
        var valuation = (await c.Valuations.ComputeForRpuAsync(c.Seed.RpuId)).Value;
        var draft = (await c.Assessments.CreateAsync(Request(valuation.Id, c.Seed.AssessmentId))).Value;
        await c.Assessments.SubmitForReviewAsync(draft.Id);
        (await c.Assessments.ApproveAsync(draft.Id)).IsSuccess.ShouldBeTrue();

        var posted = await c.Assessments.PostAsync(draft.Id);

        posted.IsSuccess.ShouldBeTrue(posted.IsSuccess ? null : posted.Message);
        posted.Value.TaxDeclarationNote.ShouldNotBeNullOrWhiteSpace();
        var prepared = await c.Db.TaxDeclarations.AnyAsync(x => x.AssessmentId == draft.Id);
        posted.Value.TaxDeclarationNote!.ShouldStartWith(prepared ? "A draft Tax Declaration" : "No Tax Declaration was prepared: ");
        (await c.Assessments.GetByIdAsync(draft.Id)).Value.TaxDeclarationNote.ShouldBeNull(); // only the posting result carries it
    }

    [Fact]
    public async Task PreviousAssessment_MustBeAPostedAssessmentOfTheSameUnit()
    {
        var (c, tx) = await BeginAsync();
        await using var _ = tx;
        var valuation = (await c.Valuations.ComputeForRpuAsync(c.Seed.RpuId)).Value;
        var draft = (await c.Assessments.CreateAsync(Request(valuation.Id))).Value;
        var other = await BillingFlowTests.SeedPostedAssessmentAsync(c.Services, c.Db, new DateOnly(2026, 1, 1));

        (await c.Assessments.PreviewAsync(Request(valuation.Id, draft.Id))).Code.ShouldBe("PREVIOUS_ASSESSMENT_INVALID");
        (await c.Assessments.PreviewAsync(Request(valuation.Id, other.AssessmentId))).Code.ShouldBe("PREVIOUS_ASSESSMENT_INVALID");
        (await c.Assessments.PreviewAsync(Request(valuation.Id, Guid.NewGuid()))).Code.ShouldBe("PREVIOUS_ASSESSMENT_NOT_FOUND");
    }

    [Fact]
    public async Task LevelBrackets_StayOpenSideBySide_AndMatchOverTheLowerNotOverTheUpper()
    {
        var (c, tx) = await BeginAsync();
        await using var _ = tx;
        var first = await c.Db.AssessmentLevels.AsNoTracking().SingleAsync(x => x.ClassificationId == c.Seed.ClassificationId);

        // A second bracket above the seeded one (0–1,000,000 at 20%).
        await LevelAsync(c, 1_000_000m, null, 30m, new DateOnly(2026, 1, 1));
        (await c.Db.AssessmentLevels.AsNoTracking().SingleAsync(x => x.Id == first.Id)).EndDate.ShouldBeNull();

        (await LevelForAreaAsync(c, 1_000m)).ShouldBe(20m); // exactly 1,000,000: not over the upper value
        (await LevelForAreaAsync(c, 1_000.5m)).ShouldBe(30m); // 1,000,500: over the lower value of the next bracket

        // A level overlapping both closes both from its effective date.
        var replacement = await LevelAsync(c, 0m, null, 25m, new DateOnly(2026, 6, 1));
        var open = await c.Db.AssessmentLevels.AsNoTracking()
            .Where(x => x.ClassificationId == c.Seed.ClassificationId && x.EndDate == null).Select(x => x.Id).ToListAsync();
        open.ShouldBe([replacement]);
        (await c.Db.AssessmentLevels.AsNoTracking().SingleAsync(x => x.Id == first.Id)).EndDate.ShouldBe(new DateOnly(2026, 5, 31));
    }

    [Fact]
    public async Task ApprovingALevel_ThatOverlapsAnApprovedOne_IsRefused()
    {
        var (c, tx) = await BeginAsync();
        await using var _ = tx;
        // Entered around the create rule (e.g. migrated data): overlaps the seeded 0–1,000,000 level in 2026.
        var overlapping = new AssessmentLevel
        {
            OrdinanceNumber = $"ORD-{Guid.NewGuid():N}", OrdinanceDate = new DateOnly(2026, 1, 1),
            ClassificationId = c.Seed.ClassificationId, ActualUseId = c.ActualUseId, PropertyTypeId = c.PropertyTypeId,
            LowerValue = 500_000m, UpperValue = 2_000_000m, AssessmentPercentage = 40m, EffectiveDate = new DateOnly(2026, 3, 1),
            Status = WorkflowStatus.Draft,
        };
        c.Db.AssessmentLevels.Add(overlapping);
        await c.Db.SaveChangesAsync();

        (await c.Levels.ApproveAsync(overlapping.Id)).Code.ShouldBe("ASSESSMENT_LEVEL_OVERLAP");
    }

    [Theory]
    [InlineData(0, 100, 100, 200, false)] // adjacent brackets share no value
    [InlineData(0, 100, 50, null, true)]
    [InlineData(0, null, 1_000_000, null, true)]
    [InlineData(200, 300, 0, 100, false)]
    public void RangesOverlap_ReadsOverTheLowerNotOverTheUpper(int lower1, int? upper1, int lower2, int? upper2, bool expected) =>
        AssessmentLevelService.RangesOverlap(lower1, upper1, lower2, upper2).ShouldBe(expected);

    [Fact]
    public async Task Smvs_AreListed()
    {
        var (c, tx) = await BeginAsync();
        await using var _ = tx;
        var smvId = await c.Db.Valuations.Where(x => x.RpuId == c.Seed.RpuId).Select(x => x.SmvId).FirstAsync();

        var list = await c.Services.GetRequiredService<ISmvService>().ListAsync(new PagedRequest { PageSize = 100 });

        list.IsSuccess.ShouldBeTrue();
        list.Value.Items.Select(x => x.Id).ShouldContain(smvId!.Value);
    }
}
