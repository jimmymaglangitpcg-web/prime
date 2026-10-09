using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Prime.Application.Features.AssessmentLevels;
using Prime.Application.Features.Assessments;
using Prime.Application.Features.Lands;
using Prime.Application.Features.Smv;
using Prime.Application.Features.Valuation;
using Prime.Domain.Enums;
using Prime.Domain.Exceptions;
using Prime.Infrastructure.Persistence;
using Prime.WebApi.Middleware;
using Shouldly;
using Xunit;

namespace Prime.IntegrationTests;

/// <summary>
/// Step H3 (docs/analysis/production-hardening.md §4.6; CLAUDE.md §75): the calculation matrix's cells that need the
/// database: a value assessed in the request that computed it equals one read back, bracket boundaries to the
/// centavo, the assessed value's rounding, the largest storable value and beyond, lines that add up to the total,
/// adjustments that would make a value negative, and the API's answer to an out-of-range amount.
/// DEMO SMV (seeded at 1,000/sqm on 500 sqm) and DEMO levels; rolled back.
/// </summary>
public class CalculationMatrixTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly DateOnly AsOf = new(2027, 1, 1);

    private sealed record Ctx(PrimeDbContext Db, IServiceProvider Services, BillingFlowTests.Seed Seed, Guid ActualUseId, Guid PropertyTypeId)
    {
        public IValuationService Valuations => Services.GetRequiredService<IValuationService>();
        public IAssessmentService Assessments => Services.GetRequiredService<IAssessmentService>();
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

    /// <summary>A DEMO level for the seeded classification; one overlapping the seeded 0–1,000,000 at 20 % closes it from its date.</summary>
    private static async Task LevelAsync(Ctx c, decimal lower, decimal? upper, decimal percentage, DateOnly effective)
    {
        var levels = c.Services.GetRequiredService<IAssessmentLevelService>();
        var created = await levels.CreateAsync(new CreateAssessmentLevelRequest(
            $"ORD-{Guid.NewGuid():N}", effective, c.Seed.ClassificationId, c.ActualUseId, c.PropertyTypeId, lower, upper, percentage, effective));
        created.IsSuccess.ShouldBeTrue(created.Message);
        (await TestSeed.AsCheckerAsync(c.Services, () => levels.ApproveAsync(created.Value.Id))).IsSuccess.ShouldBeTrue();
    }

    /// <summary>Sets the land's area and the seeded SMV's rate, bypassing the screens' rules.</summary>
    private static async Task SetAsync(Ctx c, decimal area, decimal rate)
    {
        await c.Db.Lands.Where(x => x.RpuId == c.Seed.RpuId).ExecuteUpdateAsync(x => x.SetProperty(l => l.Area, area));
        await c.Db.SmvSchedules.Where(x => x.SmvId == c.Seed.SmvId).ExecuteUpdateAsync(x => x.SetProperty(s => s.MarketValue, rate));
        c.Db.ChangeTracker.Clear(); // ExecuteUpdate bypasses tracked entities
    }

    private static CreateAssessmentRequest Request(Guid valuationId) => new(valuationId, 2027, AsOf, null, null, "DEMO reassessment");

    /// <summary>Values the unit and previews its assessment in the same request, as a general revision does.</summary>
    private static async Task<(ValuationDto Valuation, AssessmentPreviewDto Assessment)> ValueAndAssessAsync(Ctx c)
    {
        var valued = await c.Valuations.ComputeForRpuAsync(c.Seed.RpuId, asOf: AsOf);
        valued.IsSuccess.ShouldBeTrue(valued.Message);
        var preview = await c.Assessments.PreviewAsync(Request(valued.Value.Id));
        preview.IsSuccess.ShouldBeTrue(preview.Message);
        return (valued.Value, preview.Value);
    }

    [Fact]
    public async Task AValueAFractionOfACentavoOverABracket_IsAssessedAsStored_InTheSameRequestAndLater()
    {
        var (c, tx) = await BeginAsync();
        await using var _ = tx;
        await LevelAsync(c, 1_000_000m, null, 30m, new DateOnly(2026, 1, 1));
        await SetAsync(c, 999.6901m, 1_000.31m); // = 1,000,000.003931

        var (valuation, sameRequest) = await ValueAndAssessAsync(c);

        var line = valuation.Lines.ShouldNotBeNull().Single();
        line.MarketValue.ShouldBe(1_000_000.00m);
        line.Breakdown.Single(b => b.Key == "MarketValueBeforeRounding").Value.ShouldBe(1_000_000.003931m);
        valuation.ComputedMarketValue.ShouldBe(1_000_000.00m);
        // Not over the upper value of the 20 % bracket: before H3 the same request read 1,000,000.003931 and took 30 %.
        sameRequest.Lines.Single().AssessmentPercentage.ShouldBe(20m);
        sameRequest.AssessedValue.ShouldBe(200_000.00m);

        c.Db.ChangeTracker.Clear();
        var later = await c.Assessments.PreviewAsync(Request(valuation.Id));
        (later.Value.Lines.Single().AssessmentPercentage, later.Value.AssessedValue).ShouldBe((20m, 200_000.00m));
    }

    [Theory]
    [InlineData("0", 20, "0")]                       // a lower value of 0 includes 0
    [InlineData("999999.99", 20, "199999.998")]
    [InlineData("1000000.00", 20, "200000.00")]      // exactly the upper value: not over it
    [InlineData("1000000.01", 30, "300000.003")]     // one centavo over: the next bracket
    public async Task BracketBoundaries_ToTheCentavo(string marketValue, int percent, string unroundedAssessed)
    {
        var (c, tx) = await BeginAsync();
        await using var _ = tx;
        await LevelAsync(c, 1_000_000m, null, 30m, new DateOnly(2026, 1, 1));
        await SetAsync(c, 1m, decimal.Parse(marketValue));

        var (_, assessment) = await ValueAndAssessAsync(c);

        assessment.Lines.Single().AssessmentPercentage.ShouldBe((decimal)percent);
        assessment.AssessedValue.ShouldBe(Math.Round(decimal.Parse(unroundedAssessed), 2, MidpointRounding.AwayFromZero));
    }

    [Theory]
    [InlineData("0.0003", "1000", "0.11")]           // 0.30 × 35 % = 0.105: half away from zero (half to even would give 0.10)
    [InlineData("1", "1234567.89", "432098.76")]     // 432,098.7615
    public async Task TheAssessedValue_RoundsToTheCentavo_HalfAwayFromZero(string area, string rate, string expected)
    {
        var (c, tx) = await BeginAsync();
        await using var _ = tx;
        await LevelAsync(c, 0m, null, 35m, AsOf);
        await SetAsync(c, decimal.Parse(area), decimal.Parse(rate));

        var (_, assessment) = await ValueAndAssessAsync(c);

        assessment.Lines.Single().AssessmentPercentage.ShouldBe(35m);
        assessment.AssessedValue.ShouldBe(decimal.Parse(expected));
    }

    [Fact]
    public async Task TheLargestStorableValue_IsStoredExactly_AndOneBeyondIsRefused_WithNothingSaved()
    {
        var (c, tx) = await BeginAsync();
        await using var _ = tx;
        await LevelAsync(c, 1_000_000m, null, 20m, new DateOnly(2026, 1, 1));
        await SetAsync(c, 1m, 9_999_999_999_999_999.99m);

        var (valuation, assessment) = await ValueAndAssessAsync(c);
        valuation.ComputedMarketValue.ShouldBe(9_999_999_999_999_999.99m);
        assessment.AssessedValue.ShouldBe(2_000_000_000_000_000.00m); // 1,999,999,999,999,999.998

        await SetAsync(c, 1.0001m, 9_999_999_999_999_999.99m);
        var before = await c.Db.Valuations.CountAsync(x => x.RpuId == c.Seed.RpuId);
        (await Should.ThrowAsync<ValueOutOfRangeException>(() => c.Valuations.ComputeForRpuAsync(c.Seed.RpuId, asOf: AsOf))).Code.ShouldBe("VALUE_OUT_OF_RANGE");
        c.Db.ChangeTracker.Clear();
        (await c.Db.Valuations.CountAsync(x => x.RpuId == c.Seed.RpuId)).ShouldBe(before);
    }

    [Fact]
    public async Task TheStoredLines_AddUpToTheStoredTotal()
    {
        var (c, tx) = await BeginAsync();
        await using var _ = tx;
        var land = await c.Db.Lands.AsNoTracking().SingleAsync(x => x.RpuId == c.Seed.RpuId);
        var added = await c.Services.GetRequiredService<ILandService>().AddStripAsync(land.Id,
            new AddLandStripRequest(c.Seed.ClassificationId, null, land.ActualUseId, null, 0.03m, DepthBand: null));
        added.IsSuccess.ShouldBeTrue(added.Message);
        await c.Db.LandStrips.Where(x => x.LandId == land.Id).ExecuteUpdateAsync(x => x.SetProperty(s => s.Area, 0.03m));
        await SetAsync(c, 0.06m, 0.13m); // each strip 0.0039: rounded, 0.00 each; the unrounded sum would round to 0.01

        var valued = await c.Valuations.ComputeForRpuAsync(c.Seed.RpuId, asOf: AsOf);
        valued.IsSuccess.ShouldBeTrue(valued.Message);
        c.Db.ChangeTracker.Clear();
        var stored = await c.Db.Valuations.AsNoTracking().Include(x => x.Lines).SingleAsync(x => x.Id == valued.Value.Id);

        stored.Lines.Select(l => l.MarketValue).ShouldBe([0m, 0m]);
        stored.ComputedMarketValue.ShouldBe(stored.Lines.Sum(l => l.MarketValue));
        valued.Value.ComputedMarketValue.ShouldBe(stored.ComputedMarketValue);
    }

    [Fact]
    public async Task Adjustments_DeductingMoreThanTheWholeValue_AreRefused()
    {
        var (c, tx) = await BeginAsync();
        await using var _ = tx;
        var factors = c.Services.GetRequiredService<IAdjustmentFactorService>();
        var lands = c.Services.GetRequiredService<ILandService>();
        var land = await c.Db.Lands.AsNoTracking().SingleAsync(x => x.RpuId == c.Seed.RpuId);
        foreach (var (code, percent) in new[] { ("H3-A", -60m), ("H3-B", -50m) })
        {
            var created = await factors.CreateAsync(new CreateAdjustmentFactorRequest(c.Seed.SmvId, code, $"DEMO {code}", percent, null, null,
                "DEMO — not an SMV provision", new DateOnly(2026, 1, 1), null, AdjustmentRuleKind.Flat, null, null, []));
            created.IsSuccess.ShouldBeTrue(created.Message);
            (await TestSeed.AsCheckerAsync(c.Services, () => factors.ApproveAsync(created.Value.Id))).IsSuccess.ShouldBeTrue();
            (await lands.AddAdjustmentAsync(land.Id, new AddLandAdjustmentRequest(code, null, null))).IsSuccess.ShouldBeTrue();
        }

        var refused = await c.Valuations.ComputeForRpuAsync(c.Seed.RpuId, asOf: AsOf);

        refused.Code.ShouldBe("ADJUSTMENT_TOTAL_OUT_OF_RANGE");
        refused.Message!.ShouldContain("H3-A -60%");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AnAmountTooLargeForDecimalOrTheColumn_IsA400_NotA500(bool overflow)
    {
        Exception thrown = overflow
            ? new OverflowException("Value was either too large or too small for a Decimal.")
            : new DbUpdateException("save failed", new PostgresException("numeric field overflow", "ERROR", "ERROR", PostgresErrorCodes.NumericValueOutOfRange));
        var context = new DefaultHttpContext { Response = { Body = new MemoryStream() } };
        var middleware = new ExceptionHandlingMiddleware(_ => throw thrown, NullLogger<ExceptionHandlingMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
        context.Response.Body.Position = 0;
        using var body = await JsonDocument.ParseAsync(context.Response.Body);
        body.RootElement.GetProperty("code").GetString().ShouldBe("VALUE_OUT_OF_RANGE");
        body.RootElement.GetProperty("message").GetString()!.ShouldNotContain("Decimal");
    }
}
