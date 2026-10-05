using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Prime.Application.Features.Smv;
using Prime.Domain.Entities;
using Prime.Domain.Entities.Identity;
using Prime.Domain.Entities.MarketData;
using Prime.Domain.Entities.Reference;
using Prime.Domain.Enums;
using Prime.Infrastructure.Identity;
using Prime.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Prime.IntegrationTests;

/// <summary>
/// Step L6-2b (docs/analysis/smv-preparation-general-revision.md §4.2): a sales analysis of one class — sales adjusted by the
/// time factors, rounded, ranged and counted, merged into sub-classes, and adopted as draft rows of the proposed SMV. DEMO sales
/// of 100 sqm whose unit prices are 2,000 … 3,300. Rolled back.
/// </summary>
public class SalesAnalysisTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private sealed record Ctx(PrimeDbContext Db, IServiceProvider Services, Guid PreparationId, Guid SmvId, Classification Class, Municipality Municipality,
        SubClassification R1, SubClassification R2)
    {
        public ISalesAnalysisService Analyses => Services.GetRequiredService<ISalesAnalysisService>();
    }

    private async Task<(Ctx Ctx, IAsyncDisposable Transaction)> BeginAsync()
    {
        var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PrimeDbContext>();
        var transaction = await db.Database.BeginTransactionAsync();
        try
        {
            // The provisional layouts, whatever versions (the LAM's) the dev database has approved.
            await TestSeed.UseReferenceFormsAsync(db);
            var user = new AppUser { SupabaseUserId = Guid.NewGuid(), DisplayName = "DEMO Preparer", Email = $"demo-{Guid.NewGuid():N}@example.invalid" };
            var province = new Province { PsgcCode = $"P{Guid.NewGuid():N}"[..10], Name = "DEMO Province" };
            var municipality = new Municipality { Province = province, PsgcCode = $"M{Guid.NewGuid():N}"[..10], Name = "DEMO Analysis Municipality" };
            var cls = new Classification { Code = $"CL{Guid.NewGuid():N}"[..8], Name = "DEMO_Residential_SA" };
            var r1 = new SubClassification { Code = $"R1{Guid.NewGuid():N}"[..8], Name = "DEMO R-1" };
            var r2 = new SubClassification { Code = $"R2{Guid.NewGuid():N}"[..8], Name = "DEMO R-2" };
            db.AddRange(user, province, municipality, cls, r1, r2);
            await db.SaveChangesAsync();
            await TestSeed.LandPropertyTypeAsync(db);
            scope.ServiceProvider.GetRequiredService<CurrentUserService>().AppUserId = user.Id;
            var p = await scope.ServiceProvider.GetRequiredService<ISmvPreparationService>().CreateAsync(new(2180 + Random.Shared.Next(0, 9),
                "DEMO preparation for SalesAnalysisTests", new DateOnly(2026, 1, 2), null, new DateOnly(2027, 1, 1), [municipality.Id], null));
            p.IsSuccess.ShouldBeTrue(p.Message);
            decimal[] prices = [200_000m, 210_000m, 220_000m, 230_000m, 250_000m, 330_000m];
            foreach (var (price, i) in prices.Select((x, i) => (x, i)))
            {
                db.MarketTransactions.Add(Sale(municipality.Id, cls.Id, new DateOnly(2026, 3, 1 + i), price));
            }
            db.MarketTransactions.Add(Sale(municipality.Id, cls.Id, new DateOnly(2026, 8, 1), 272_727.27m));      // 2,727.27/sqm in August
            db.MarketTransactions.Add(Sale(municipality.Id, cls.Id, new DateOnly(2026, 3, 20), 900_000m, withBuilding: true));
            db.MarketTransactions.Add(Sale(municipality.Id, cls.Id, new DateOnly(2026, 3, 21), 1m, MarketDataReview.Unreviewed));
            await db.SaveChangesAsync();
            return (new Ctx(db, scope.ServiceProvider, p.Value.Id, p.Value.ProposedSmv.Id, cls, municipality, r1, r2), new Scoped(transaction, scope));
        }
        catch
        {
            await transaction.DisposeAsync();
            scope.Dispose();
            throw;
        }
    }

    private static MarketTransaction Sale(Guid municipalityId, Guid classificationId, DateOnly on, decimal price,
        MarketDataReview review = MarketDataReview.Accepted, bool withBuilding = false) => new()
    {
        Source = MarketDataSource.RegistryAbstract, TransactionDate = on, MunicipalityId = municipalityId, ClassificationId = classificationId,
        LandArea = 100m, Consideration = price, ConveysBuilding = withBuilding, BuildingFloorArea = withBuilding ? 50m : null,
        Review = review, ReviewedAt = review == MarketDataReview.Unreviewed ? null : DateTimeOffset.UtcNow,
    };

    private sealed class Scoped(IDbContextTransaction transaction, IServiceScope scope) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            await transaction.DisposeAsync();
            scope.Dispose();
        }
    }

    [Fact]
    public async Task Analysis_AdjustsRoundsRangesAndCounts_ThenAdoptsSubClassValuesIntoTheProposedSmv()
    {
        var (c, tx) = await BeginAsync();
        await using var _ = tx;
        var created = await c.Analyses.CreateAsync(c.PreparationId, new(c.Class.Id, null, [c.Municipality.Id], null, null, AreaMeasure.SquareMetre, 100m, 6m, null));
        created.IsSuccess.ShouldBeTrue(created.Message);
        var a = created.Value;
        // Accepted land sales only; the land part of the one with a building is unknown. No factor yet: prices as recorded.
        a.Sales.Count.ShouldBe(8);
        a.Sales.Single(s => s.Price == null).ExclusionReason.ShouldBe("The land's price cannot be told apart from the building's.");
        a.Warnings.ShouldContain(w => w.Contains("No time-adjustment factor"));
        a.Values.Select(v => v.RoundedUnitValue).ShouldBe([2000m, 2100m, 2200m, 2300m, 2500m, 2700m, 3300m]);

        // A factor for the first half only: the August sale is not covered.
        (await c.Analyses.AddTimeFactorAsync(c.PreparationId, new(new DateOnly(2026, 1, 1), new DateOnly(2026, 6, 30), 1m, "DEMO index"))).IsSuccess.ShouldBeTrue();
        (await c.Analyses.AddTimeFactorAsync(c.PreparationId, new(new DateOnly(2026, 6, 1), new DateOnly(2026, 9, 30), 1.1m, "DEMO")))
            .Code.ShouldBe("TIME_FACTOR_OVERLAP");
        a = (await c.Analyses.GetAsync(a.Id)).Value;
        a.Sales.Single(s => s.TransactionDate == new DateOnly(2026, 8, 1)).ExclusionReason.ShouldBe("No time-adjustment factor covers 2026-08-01.");
        (await c.Analyses.AddTimeFactorAsync(c.PreparationId, new(new DateOnly(2026, 7, 1), new DateOnly(2026, 12, 31), 1.1m, "DEMO index"))).IsSuccess.ShouldBeTrue();
        a = (await c.Analyses.GetAsync(a.Id)).Value;
        var august = a.Sales.Single(s => s.TransactionDate == new DateOnly(2026, 8, 1));
        (august.TimeFactor, august.AdjustedUnitPrice, august.RoundedUnitValue).ShouldBe(((decimal?)1.1m, (decimal?)3000m, (decimal?)3000m));

        // Table 1, the average interval, and Table 2 at ±6%.
        a.Values.Select(v => v.RoundedUnitValue).ShouldBe([2000m, 2100m, 2200m, 2300m, 2500m, 3000m, 3300m]);
        a.Values[1].IntervalPercent.ShouldBe(5m);
        a.EffectiveWidthPercent.ShouldBe(6m);
        a.Ranges.Select(r => (r.Number, r.Low, r.Mid, r.High, r.Frequency)).ShouldBe([
            (1, 2000m, 2100m, 2226m, 3), (2, 2300m, 2400m, 2544m, 2), (3, 3000m, 3200m, 3392m, 2)]);

        // Leaving a sale out needs a reason; the assessor can put it back.
        var first = a.Sales.First(s => s.RoundedUnitValue == 2000m);
        (await c.Analyses.UpdateSaleAsync(a.Id, first.Id, new(true, null, 0m, null))).Code.ShouldBe("VALIDATION_FAILED");
        (await c.Analyses.UpdateSaleAsync(a.Id, first.Id, new(true, "DEMO: related parties", 0m, null))).Value.Values.Count.ShouldBe(6);
        (await c.Analyses.UpdateSaleAsync(a.Id, first.Id, new(false, null, 0m, null))).Value.Values.Count.ShouldBe(7);

        // Ranges 2 and 3 merged into R-1, range 1 alone as R-2; highest first.
        (await c.Analyses.SetGroupsAsync(a.Id, new([new(2000m, 2400m, c.R2.Id, null, null), new(2300m, 3392m, c.R1.Id, null, null)])))
            .Code.ShouldBe("SALES_ANALYSIS_GROUP_OVERLAP");
        a = (await c.Analyses.SetGroupsAsync(a.Id, new([
            new(2000m, 2226m, c.R2.Id, 2100m, "DEMO: range 1"), new(2300m, 3392m, c.R1.Id, 2800m, "DEMO: ranges 2–3 merged")]))).Value;
        a.Groups.Select(g => (g.Sequence, g.SubClassificationName, g.Frequency, g.ProposedValue)).ShouldBe([
            (1, "DEMO R-1", 4, (decimal?)2800m), (2, "DEMO R-2", 3, (decimal?)2100m)]);
        a.Ranges.Select(r => r.GroupId).ShouldBe([a.Groups[1].Id, a.Groups[0].Id, a.Groups[0].Id]);

        var adopted = await c.Analyses.AdoptGroupAsync(a.Id, a.Groups[0].Id);
        adopted.IsSuccess.ShouldBeTrue(adopted.Message);
        var row = await c.Db.SmvSchedules.AsNoTracking().SingleAsync(x => x.Id == adopted.Value.Groups[0].SmvScheduleId);
        (row.SmvId, row.ClassificationId, row.SubClassificationId, row.MarketValue, row.Unit, row.Status, row.EffectiveDate)
            .ShouldBe((c.SmvId, c.Class.Id, (Guid?)c.R1.Id, 2800m, "per sqm", WorkflowStatus.Draft, new DateOnly(2027, 1, 1)));
        (await c.Analyses.AdoptGroupAsync(a.Id, a.Groups[0].Id)).Code.ShouldBe("SALES_ANALYSIS_GROUP_ADOPTED");
        // Replacing the groups keeps the adopted one, and may not overlap it.
        (await c.Analyses.SetGroupsAsync(a.Id, new([new(2000m, 2400m, c.R2.Id, 2100m, null)]))).Code.ShouldBe("SALES_ANALYSIS_GROUP_OVERLAP");
        (await c.Analyses.SetGroupsAsync(a.Id, new([]))).Value.Groups.Count.ShouldBe(1);

        // Forms 2–4 print the analysis as it stands (L6-2c).
        var forms = c.Services.GetRequiredService<Prime.Application.Features.Forms.IFormService>();
        foreach (var (code, text) in new[] { ("SMV_FORM_2", "2,000.00"), ("SMV_FORM_3", "3,000.00"), ("SMV_FORM_4", "2,800.00") })
        {
            var preview = await forms.PreviewAsync(code, a.Id);
            preview.IsSuccess.ShouldBeTrue($"{code}: {preview.Message}");
            preview.Value.Html.ShouldContain(text);
        }

        // After submission the analysis no longer changes.
        (await c.Services.GetRequiredService<ISmvPreparationService>().RecordEventAsync(c.PreparationId,
            new(SmvPreparationEventKind.SubmittedToRegionalOffice, new DateOnly(2026, 9, 1), null, null))).IsSuccess.ShouldBeTrue();
        (await c.Analyses.UpdateSaleAsync(a.Id, first.Id, new(true, "DEMO", 0m, null))).Code.ShouldBe("SMV_PREPARATION_STAGE");
        (await c.Analyses.GetAsync(a.Id)).Value.Editable.ShouldBeFalse();
    }

    [Fact]
    public async Task Refresh_AddsNewAcceptedSales_AndLeavesOutThoseNoLongerAccepted_AndAClassIsAnalysedOnce()
    {
        var (c, tx) = await BeginAsync();
        await using var _ = tx;
        var a = (await c.Analyses.CreateAsync(c.PreparationId, new(c.Class.Id, null, [c.Municipality.Id], null, null, AreaMeasure.SquareMetre, null, null, null))).Value;
        (await c.Analyses.CreateAsync(c.PreparationId, new(c.Class.Id, null, [c.Municipality.Id], null, null, AreaMeasure.SquareMetre, null, null, null)))
            .Code.ShouldBe("SALES_ANALYSIS_DUPLICATE");
        c.Db.MarketTransactions.Add(Sale(c.Municipality.Id, c.Class.Id, new DateOnly(2026, 4, 1), 260_000m));
        var withdrawn = await c.Db.MarketTransactions.FirstAsync(x => x.ClassificationId == c.Class.Id && x.Consideration == 330_000m);
        (withdrawn.Review, withdrawn.ExclusionReason) = (MarketDataReview.Excluded, "DEMO: found not at arm's length");
        await c.Db.SaveChangesAsync();

        var refreshed = (await c.Analyses.RefreshAsync(a.Id)).Value;
        refreshed.Sales.Count.ShouldBe(9);
        refreshed.Sales.Single(s => s.Price == 330_000m).ExclusionReason.ShouldBe("No longer an accepted sale in the market data.");
        refreshed.Values.Select(v => v.RoundedUnitValue).ShouldContain(2600m);
        // Without a width the average interval is used.
        refreshed.EffectiveWidthPercent.ShouldBe(refreshed.AverageIntervalPercent);
    }
}
