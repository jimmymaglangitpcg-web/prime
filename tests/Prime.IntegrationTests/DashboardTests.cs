using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Prime.Application.Common.Interfaces;
using Prime.Application.Features.Dashboard;
using Prime.Application.Features.Reports;
using Prime.Domain.Entities;
using Prime.Domain.Entities.Reference;
using Prime.Domain.Enums;
using Prime.Infrastructure.Identity;
using Prime.Infrastructure.Persistence;
using Prime.WebApi.Authentication;
using Shouldly;
using Xunit;

namespace Prime.IntegrationTests;

/// <summary>
/// Phase 11 step R2 (docs/analysis/reporting.md §4.3, §7 item 5): the dashboard shows the FAAS in force in the user's
/// jurisdiction, zeros where there is nothing, and caches its figures for a minute per jurisdiction. Each test restricts the
/// request to a fresh DEMO municipality, so the shared database's other records do not count, and is rolled back.
/// </summary>
public class DashboardTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private sealed record Ctx(PrimeDbContext Db, IServiceProvider Services, BillingFlowTests.Seed Seed, Guid MunicipalityId)
    {
        public IDashboardService Dashboard => Services.GetRequiredService<IDashboardService>();
    }

    /// <summary>The seeded land unit (approved TD declaring the posted assessment: MV 500,000; AV 100,000; 500 sqm), its municipality only.</summary>
    private async Task<(Ctx Ctx, IAsyncDisposable Scope)> BeginAsync()
    {
        var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PrimeDbContext>();
        var transaction = await db.Database.BeginTransactionAsync();
        var seed = await BillingFlowTests.SeedPostedAssessmentAsync(scope.ServiceProvider, db, new DateOnly(2026, 1, 1), Taxability.Taxable);
        var td = await db.TaxDeclarations.SingleAsync(x => x.Id == seed.TaxDeclaration.Id);
        td.AssessmentId = seed.AssessmentId;
        td.ApprovedAt = DateTimeOffset.UtcNow.AddDays(-30);
        await db.SaveChangesAsync();
        var municipalityId = await db.Properties.Where(p => p.Id == seed.PropertyId).Select(p => p.MunicipalityId).SingleAsync();
        scope.ServiceProvider.GetRequiredService<JurisdictionState>().Restrict([municipalityId]);
        return (new Ctx(db, scope.ServiceProvider, seed, municipalityId), new Disposer(transaction, scope));
    }

    private sealed class Disposer(IAsyncDisposable transaction, IAsyncDisposable scope) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            await transaction.DisposeAsync();
            await scope.DisposeAsync();
        }
    }

    [Fact]
    public async Task TheDashboard_ShowsTheFaasInForce_InTheUsersJurisdiction()
    {
        var (c, scope) = await BeginAsync();
        await using var _ = scope;

        var d = await c.Dashboard.GetAsync();

        d.AsOf.ShouldBe(c.Services.GetRequiredService<IClock>().Today);
        d.Figures.Properties.ShouldBe(1);
        d.Figures.Parcels.ShouldBe(await c.Db.Parcels.CountAsync(x => x.PropertyId == c.Seed.PropertyId));
        (d.Figures.PropertiesWithFaas, d.Figures.UnitsInForce, d.Figures.LandAreaSqm).ShouldBe((1, 1, 500m));
        (d.Figures.TaxableMarketValue, d.Figures.TaxableAssessedValue).ShouldBe((500_000m, 100_000m));
        (d.Figures.ExemptMarketValue, d.Figures.ExemptAssessedValue).ShouldBe((0m, 0m));
        var byClass = d.ByClassification.ShouldHaveSingleItem();
        (byClass.Key, byClass.Properties, byClass.AssessedValue).ShouldBe((c.Seed.TaxDeclaration.ClassificationId, 1, 100_000m));
        var byBarangay = d.ByBarangay.ShouldHaveSingleItem();
        (byBarangay.Label, byBarangay.Detail, byBarangay.MarketValue).ShouldBe(("Demo Barangay", "Demo Municipality", 500_000m));
        d.RecentApprovals.ShouldContain(a => a.Kind == "TaxDeclaration" && a.Id == c.Seed.TaxDeclaration.Id);
        d.RecentApprovals.ShouldAllBe(a => a.PropertyId == c.Seed.PropertyId);
        d.RecentTransactions.ShouldAllBe(t => t.PropertyId == c.Seed.PropertyId);
    }

    [Fact]
    public async Task AJurisdictionWithNoRecords_ShowsZeros_NotSampleFigures()
    {
        var (c, scope) = await BeginAsync();
        await using var _ = scope;
        var empty = new Municipality { ProvinceId = await c.Db.Municipalities.Where(m => m.Id == c.MunicipalityId).Select(m => m.ProvinceId).SingleAsync(),
            PsgcCode = $"M{Guid.NewGuid():N}"[..10], Name = "DEMO Empty" };
        c.Db.Municipalities.Add(empty);
        await c.Db.SaveChangesAsync();
        c.Services.GetRequiredService<JurisdictionState>().Restrict([empty.Id]);

        var d = await c.Dashboard.GetAsync();

        d.Figures.ShouldBe(new DashboardFiguresDto(0, 0, 0, 0, 0m, 0, 0m, 0m, 0m, 0m));
        d.ByClassification.ShouldBeEmpty();
        d.ByBarangay.ShouldBeEmpty();
        d.GeneralRevisions.ShouldBeEmpty();
        d.RecentTransactions.ShouldBeEmpty();
        d.RecentApprovals.ShouldBeEmpty();
    }

    [Fact]
    public async Task TheFigures_AreCachedForAMinute_PerJurisdiction()
    {
        var (c, scope) = await BeginAsync();
        await using var _ = scope;
        var first = await c.Dashboard.GetAsync();

        c.Db.Properties.Add(new PropertyEntity
        {
            PropertyIdentificationNumber = $"DEMO-{Guid.NewGuid():N}"[..20], ProvinceId = c.Db.Properties.Where(p => p.Id == c.Seed.PropertyId).Select(p => p.ProvinceId).Single(),
            MunicipalityId = c.MunicipalityId, BarangayId = c.Db.Properties.Where(p => p.Id == c.Seed.PropertyId).Select(p => p.BarangayId).Single(),
        });
        await c.Db.SaveChangesAsync();
        var again = await c.Dashboard.GetAsync();

        again.Figures.Properties.ShouldBe(1, "within the minute the cached figures are shown");
        again.ComputedAt.ShouldBe(first.ComputedAt);
        DashboardService.CacheDuration.ShouldBe(TimeSpan.FromMinutes(1));
    }

    [Fact]
    public void AChart_ShowsTheTenLargestGroups_AndSumsTheRestAsOthers()
    {
        var groups = Enumerable.Range(1, 13).Select(i => new FaasGroup
        {
            GroupBy = FaasGroupBy.Barangay, Key = Guid.NewGuid(), Properties = i, TaxableAssessedValue = i * 1_000m, TaxableMarketValue = i * 5_000m,
        }).ToList();

        var bars = DashboardService.Chart(groups, _ => ("DEMO Barangay", null));

        bars.Count.ShouldBe(DashboardService.ChartGroups + 1);
        bars[0].AssessedValue.ShouldBe(13_000m);
        var others = bars[^1];
        (others.IsOthers, others.Label, others.Properties, others.AssessedValue, others.MarketValue).ShouldBe((true, "Others (3)", 6, 6_000m, 30_000m));
        DashboardService.Chart(groups.Take(11), _ => ("DEMO", null)).ShouldNotContain(b => b.IsOthers, "eleven groups fit without an Others bar");
    }

    [Fact]
    public async Task TheApi_ShowsTheDashboard_ToAnyUserOfPrime()
    {
        var client = factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/dashboard");
        request.Headers.Add(DevelopmentAuthenticationHandler.ActAsHeader, "viewer");

        var response = await client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
