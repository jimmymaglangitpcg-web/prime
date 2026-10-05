using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NetTopologySuite;
using NetTopologySuite.Geometries;
using Prime.Application.Features.Gis;
using Prime.Application.Features.Gis.ReferenceLayers;
using Prime.Application.Features.Smv;
using Prime.Domain.Entities;
using Prime.Infrastructure.Identity;
using Prime.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Prime.IntegrationTests.Gis;

/// <summary>
/// Step L6-4 (docs/analysis/smv-preparation-general-revision.md §4.4): the land value map — each land parcel with the unit value
/// its land takes under the approved SMV in force or a proposed one — and the sub-market areas layer. DEMO data at a random origin;
/// rolled back.
/// </summary>
public class LandValueMapTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly GeometryFactory Wgs84 = NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);

    private static MultiPolygon Square(double lon, double lat, double size = 0.001) =>
        Wgs84.CreateMultiPolygon([Wgs84.CreatePolygon([
            new Coordinate(lon, lat), new Coordinate(lon + size, lat), new Coordinate(lon + size, lat + size), new Coordinate(lon, lat + size), new Coordinate(lon, lat),
        ])]);

    private static string Bbox(double lon, double lat, double size = 0.01) =>
        string.Create(CultureInfo.InvariantCulture, $"{lon - size},{lat - size},{lon + size},{lat + size}");

    [Fact]
    public async Task EachLandParcel_TakesTheUnitValueOfItsLand_UnderTheSmvInForce_OrAProposedOne()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PrimeDbContext>();
        await using var tx = await db.Database.BeginTransactionAsync();
        scope.ServiceProvider.GetRequiredService<CurrentUserService>().AppUserId = null;
        var seed = await BillingFlowTests.SeedPostedAssessmentAsync(scope.ServiceProvider, db, new DateOnly(2026, 1, 1));
        var property = await db.Properties.SingleAsync(x => x.Id == seed.PropertyId);
        var (lon, lat) = (120.0 + Random.Shared.NextDouble() * 2, 10.0 + Random.Shared.NextDouble() * 5);
        // A second property in the same place, with no land recorded.
        var bare = new PropertyEntity
        {
            PropertyIdentificationNumber = $"PIN-{Guid.NewGuid():N}", ProvinceId = property.ProvinceId, MunicipalityId = property.MunicipalityId, BarangayId = property.BarangayId,
        };
        db.Properties.Add(bare);
        db.Parcels.AddRange(
            new Parcel { PropertyId = property.Id, BarangayId = property.BarangayId, Geometry = Square(lon, lat) },
            new Parcel { Property = bare, BarangayId = property.BarangayId, Geometry = Square(lon + 0.002, lat) });
        await db.SaveChangesAsync();

        // A proposed SMV, a draft, at 1,500 for the same class and use from 2027.
        var smvs = scope.ServiceProvider.GetRequiredService<ISmvService>();
        var proposed = (await smvs.CreateSmvAsync(new CreateSmvRequest($"DEMO-VM-{Guid.NewGuid():N}"[..24], new DateOnly(2026, 12, 1), null,
            new DateOnly(2027, 1, 1), 2027, "DEMO proposed SMV for LandValueMapTests"))).Value;
        var row = await db.SmvSchedules.AsNoTracking().SingleAsync(x => x.SmvId == seed.SmvId);
        (await smvs.CreateScheduleAsync(proposed.Id, new CreateSmvScheduleRequest(row.ClassificationId, row.ActualUseId, row.PropertyTypeId, null, "per sqm",
            1_500m, null, null, new DateOnly(2027, 1, 1)))).IsSuccess.ShouldBeTrue();

        var map = scope.ServiceProvider.GetRequiredService<ILandValueMapService>();
        var current = (await map.GetAsync(Bbox(lon, lat), null, new DateOnly(2026, 6, 1), null)).Value;
        current.Features.Count.ShouldBe(2);
        var land = current.Features.Single(f => f.Properties.PropertyId == property.Id).Properties;
        (land.Classification, land.UnitValue, land.Unit, land.Problem).ShouldBe(("DEMO_Residential", (decimal?)1_000m, "per sqm", (string?)null));
        current.Features.Single(f => f.Properties.PropertyId == bare.Id).Properties.Problem.ShouldBe("No land is recorded for the property.");
        current.Legend.Single().ShouldBe(new LandValueLegendItem("DEMO_Residential", null, 1, 1_000m, 1_000m, "per sqm"));

        var underProposed = (await map.GetAsync(Bbox(lon, lat), proposed.Id, new DateOnly(2027, 1, 1), null)).Value;
        var p = underProposed.Features.Single(f => f.Properties.PropertyId == property.Id).Properties;
        (p.UnitValue, p.SmvReference).ShouldBe(((decimal?)1_500m, proposed.OrdinanceNumber));
        // Before the proposed rows take effect there is no value under that SMV.
        (await map.GetAsync(Bbox(lon, lat), proposed.Id, new DateOnly(2026, 6, 1), null)).Value.Features
            .Single(f => f.Properties.PropertyId == property.Id).Properties.Problem.ShouldNotBeNull();
        (await map.GetAsync(Bbox(lon, lat), Guid.NewGuid(), null, null)).Code.ShouldBe("SMV_NOT_FOUND");
        (await map.GetAsync("bad", null, null, null)).Code.ShouldBe("INVALID_BBOX");
    }

    [Fact]
    public async Task SubMarketAreas_AreAKeyedPolygonLayer()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PrimeDbContext>();
        await using var tx = await db.Database.BeginTransactionAsync();
        var layers = scope.ServiceProvider.GetRequiredService<IReferenceLayerService>();
        var (lon, lat) = (120.0 + Random.Shared.NextDouble() * 2, 10.0 + Random.Shared.NextDouble() * 5);
        var coordinates = string.Create(CultureInfo.InvariantCulture,
            $"[[[{lon},{lat}],[{lon + 0.01},{lat}],[{lon + 0.01},{lat + 0.01}],[{lon},{lat + 0.01}],[{lon},{lat}]]]");
        var collection = JsonDocument.Parse($$$"""
            {"type":"FeatureCollection","features":[{"type":"Feature","properties":{"code":"DEMO-SMA-1","name":"DEMO R-2 north"},
             "geometry":{"type":"Polygon","coordinates":{{{coordinates}}}}}]}
            """).RootElement.Clone();
        var imported = await layers.ImportAsync(ReferenceLayer.SubMarketAreas, new(new DateOnly(2026, 1, 1), "DEMO drawn areas", null, collection), dryRun: false);
        imported.IsSuccess.ShouldBeTrue(imported.Message);
        imported.Value.Errors.ShouldBeEmpty();
        var read = (await layers.GetFeaturesAsync(ReferenceLayer.SubMarketAreas, Bbox(lon, lat, 0.05), new DateOnly(2026, 6, 1), null)).Value;
        read.Features.Single().Properties.Name.ShouldBe("DEMO R-2 north");
    }
}
