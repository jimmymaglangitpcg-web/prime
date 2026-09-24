using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using NetTopologySuite;
using NetTopologySuite.Geometries;
using Prime.Application.Features.Gis;
using Prime.Application.Features.Parcels;
using Prime.Domain.Entities;
using Prime.Domain.Entities.Audit;
using Prime.Domain.Entities.Reference;
using Prime.Domain.Enums;
using Prime.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Prime.IntegrationTests.Gis;

/// <summary>
/// Phase 7 step 2: GIS extent/point queries and parcel geometry replacement,
/// exercised through the real Application services against real PostGIS.
/// Services and the test's PrimeDbContext come from one DI scope, so they
/// share the never-committed test transaction (no cleanup needed).
/// </summary>
public class GisQueryTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly GeometryFactory Wgs84 = NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);

    // Each test works around its own random origin so rows from other tests
    // or the dev database can't land in its extent.
    private static (double Lon, double Lat) RandomOrigin() =>
        (120.0 + Random.Shared.NextDouble() * 2, 10.0 + Random.Shared.NextDouble() * 5);

    private static MultiPolygon Square(double lon, double lat, double size = 0.001) =>
        Wgs84.CreateMultiPolygon([Wgs84.CreatePolygon([
            new Coordinate(lon, lat),
            new Coordinate(lon + size, lat),
            new Coordinate(lon + size, lat + size),
            new Coordinate(lon, lat + size),
            new Coordinate(lon, lat),
        ])]);

    private static string Bbox(double minLon, double minLat, double maxLon, double maxLat) =>
        FormattableString.Invariant($"{minLon},{minLat},{maxLon},{maxLat}");

    [Fact]
    public async Task Extent_ReturnsOnlyActiveIntersectingParcels_AsGeoJson()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PrimeDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var gis = scope.ServiceProvider.GetRequiredService<IGisService>();

        var (lon, lat) = RandomOrigin();
        var inside = await AddParcelAsync(db, Square(lon, lat));
        var subdivided = await AddParcelAsync(db, Square(lon + 0.002, lat), RecordStatus.Subdivided);
        await AddParcelAsync(db, Square(lon + 0.5, lat + 0.5)); // well outside the extent

        var result = await gis.GetParcelsInExtentAsync(Bbox(lon - 0.001, lat - 0.001, lon + 0.004, lat + 0.002), null);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Type.ShouldBe("FeatureCollection");
        result.Value.Truncated.ShouldBeFalse();
        result.Value.Features.Select(f => f.Id).ShouldBe([inside.Id]);
        result.Value.Features.ShouldNotContain(f => f.Id == subdivided.Id);

        var feature = result.Value.Features.Single();
        feature.Type.ShouldBe("Feature");
        feature.Geometry.GetProperty("type").GetString().ShouldBe("MultiPolygon");
        var firstVertex = feature.Geometry.GetProperty("coordinates")[0][0][0];
        firstVertex[0].GetDouble().ShouldBe(lon, 1e-9);
        firstVertex[1].GetDouble().ShouldBe(lat, 1e-9);
        feature.Properties.PropertyId.ShouldBe(inside.PropertyId);
        feature.Properties.BarangayName.ShouldBe("Demo Barangay");
    }

    [Fact]
    public async Task Extent_OverLimit_IsTruncatedAndFlagged()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PrimeDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var gis = scope.ServiceProvider.GetRequiredService<IGisService>();

        var (lon, lat) = RandomOrigin();
        for (var i = 0; i < 3; i++)
        {
            await AddParcelAsync(db, Square(lon + i * 0.002, lat));
        }

        var result = await gis.GetParcelsInExtentAsync(Bbox(lon - 0.001, lat - 0.001, lon + 0.01, lat + 0.002), limit: 2);

        result.Value.Features.Count.ShouldBe(2);
        result.Value.Truncated.ShouldBeTrue();
        result.Value.Limit.ShouldBe(2);
    }

    [Fact]
    public async Task Point_FindsContainingParcel_AndBothNeighboursOnSharedEdge()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PrimeDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var gis = scope.ServiceProvider.GetRequiredService<IGisService>();

        var (lon, lat) = RandomOrigin();
        var west = await AddParcelAsync(db, Square(lon, lat));
        var east = await AddParcelAsync(db, Square(lon + 0.001, lat)); // shares west's eastern edge

        var inWest = await gis.GetParcelsAtPointAsync(lon + 0.0005, lat + 0.0005);
        var onEdge = await gis.GetParcelsAtPointAsync(lon + 0.001, lat + 0.0005);
        var nowhere = await gis.GetParcelsAtPointAsync(lon - 0.01, lat - 0.01);

        inWest.Value.Features.Select(f => f.Id).ShouldBe([west.Id]);
        onEdge.Value.Features.Select(f => f.Id).OrderBy(id => id).ShouldBe(new[] { west.Id, east.Id }.OrderBy(id => id));
        nowhere.Value.Features.ShouldBeEmpty();
    }

    [Fact]
    public async Task SetGeometry_Replacement_RequiresReason_AndAuditsOldBoundary()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PrimeDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var parcels = scope.ServiceProvider.GetRequiredService<IParcelService>();

        var (lon, lat) = RandomOrigin();
        var parcel = await AddParcelAsync(db, Square(lon, lat));
        var oldWkt = parcel.Geometry!.AsText();
        var current = (await parcels.GetByIdAsync(parcel.Id)).Value;
        var newWkt = Square(lon, lat, 0.002).GetGeometryN(0).AsText(); // bare POLYGON — normalized server-side

        var withoutReason = await parcels.SetGeometryAsync(parcel.Id, new SetParcelGeometryRequest(newWkt, current.Version, null));
        withoutReason.Code.ShouldBe("VALIDATION_FAILED");

        var updated = await parcels.SetGeometryAsync(parcel.Id, new SetParcelGeometryRequest(newWkt, current.Version, "Resurvey correction"));

        updated.IsSuccess.ShouldBeTrue(updated.Message);
        updated.Value.GeometryWkt.ShouldStartWith("MULTIPOLYGON");
        updated.Value.MeasuredArea!.Value.ShouldBeGreaterThan(current.MeasuredArea!.Value * 3.9m);

        var audit = await db.Set<AuditLog>()
            .Where(a => a.RecordId == parcel.Id && a.Action == AuditAction.Update)
            .SingleAsync();
        audit.Reason.ShouldBe("Resurvey correction");
        audit.OldValue.ShouldNotBeNull();
        audit.OldValue.ShouldContain(oldWkt);
    }

    [Fact]
    public async Task SetGeometry_StaleVersion_IsRejected_AndNothingChanges()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PrimeDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var parcels = scope.ServiceProvider.GetRequiredService<IParcelService>();

        var (lon, lat) = RandomOrigin();
        var parcel = await AddParcelAsync(db, Square(lon, lat));
        var originalWkt = parcel.Geometry!.AsText();
        var current = (await parcels.GetByIdAsync(parcel.Id)).Value;

        var result = await parcels.SetGeometryAsync(
            parcel.Id,
            new SetParcelGeometryRequest(Square(lon, lat, 0.002).AsText(), unchecked(current.Version + 1), "Should not apply"));

        result.Code.ShouldBe("PARCEL_CONCURRENCY_CONFLICT");
        db.ChangeTracker.Clear();
        (await db.Parcels.SingleAsync(p => p.Id == parcel.Id)).Geometry!.AsText().ShouldBe(originalWkt);
    }

    [Fact]
    public async Task SetGeometry_OnHistoricalParcel_IsRejected()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PrimeDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var parcels = scope.ServiceProvider.GetRequiredService<IParcelService>();

        var (lon, lat) = RandomOrigin();
        var parcel = await AddParcelAsync(db, Square(lon, lat), RecordStatus.Superseded);

        var result = await parcels.SetGeometryAsync(parcel.Id, new SetParcelGeometryRequest(Square(lon, lat, 0.002).AsText(), parcel.Version, "Edit history"));

        result.Code.ShouldBe("PARCEL_NOT_ACTIVE");
    }

    [Theory]
    [InlineData("/api/gis/parcels")]
    [InlineData("/api/gis/parcels?bbox=1,2,3")]
    [InlineData("/api/gis/parcels?bbox=122,14,121,15")]
    [InlineData("/api/gis/parcels?bbox=121,14,200,15")]
    [InlineData("/api/gis/parcels/at?lon=500&lat=14")]
    public async Task Http_InvalidExtentOrPoint_Returns400WithErrorCode(string url)
    {
        var response = await factory.CreateClient().GetAsync(url);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.ShouldContain("\"code\":\"INVALID_");
    }

    [Fact]
    public async Task Http_EmptyExtent_ReturnsGeoJsonFeatureCollectionShape()
    {
        // Middle of the Pacific — nothing is mapped there, so this read
        // touches no data and needs no cleanup.
        var response = await factory.CreateClient().GetAsync("/api/gis/parcels?bbox=-170,-10,-169.9,-9.9");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        json.RootElement.GetProperty("type").GetString().ShouldBe("FeatureCollection");
        json.RootElement.GetProperty("features").GetArrayLength().ShouldBe(0);
        json.RootElement.GetProperty("truncated").GetBoolean().ShouldBeFalse();
        json.RootElement.GetProperty("limit").GetInt32().ShouldBe(GisService.DefaultFeatureLimit);
    }

    [Fact]
    public async Task SpatialFilter_TranslatesToIndexableSql_AndPlannerCanUseGistIndex()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PrimeDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync();

        // 1. The filter really runs in PostGIS (not client-side evaluation).
        var sql = GisService.ActiveParcelsIntersecting(db.Parcels, Square(121, 14)).ToQueryString();
        sql.ShouldContain("ST_Intersects");

        // 2. With sequential scans disabled (the dev table is too small for
        // the planner to prefer an index on its own), the same predicate is
        // served by the GiST index — i.e. the index is usable for it.
        await db.Database.ExecuteSqlRawAsync("SET LOCAL enable_seqscan = off");
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.Transaction = db.Database.CurrentTransaction!.GetDbTransaction();
        command.CommandText = """
            EXPLAIN SELECT "Id" FROM "Parcels"
            WHERE "Status" = 'Active' AND "Geometry" IS NOT NULL
              AND ST_Intersects("Geometry", ST_GeomFromText('POLYGON((121 14,121.01 14,121.01 14.01,121 14.01,121 14))', 4326))
            """;
        var plan = new List<string>();
        await using (var reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                plan.Add(reader.GetString(0));
            }
        }

        string.Join(Environment.NewLine, plan).ShouldContain("IX_Parcels_Geometry");
    }

    [Fact]
    public async Task Http_OpenApiDocument_StillGenerates_WithGisEndpoints()
    {
        // JsonElement geometry in the contract must not break Swagger.
        var response = await factory.CreateClient().GetAsync("/swagger/v1/swagger.json");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.ShouldContain("/api/gis/parcels");
        body.ShouldContain("/api/Parcels/{id}/geometry", Case.Insensitive);
    }

    private static async Task<Parcel> AddParcelAsync(PrimeDbContext db, MultiPolygon geometry, RecordStatus status = RecordStatus.Active)
    {
        var province = new Province { PsgcCode = $"P{Guid.NewGuid():N}"[..10], Name = "Demo Province" };
        var municipality = new Municipality { Province = province, PsgcCode = $"M{Guid.NewGuid():N}"[..10], Name = "Demo Municipality" };
        var barangay = new Barangay { Municipality = municipality, PsgcCode = $"B{Guid.NewGuid():N}"[..10], Name = "Demo Barangay" };
        var property = new PropertyEntity
        {
            PropertyIdentificationNumber = $"PIN-{Guid.NewGuid():N}",
            Province = province,
            Municipality = municipality,
            Barangay = barangay,
        };
        var parcel = new Parcel { Property = property, Barangay = barangay, Geometry = geometry, Status = status };
        db.Parcels.Add(parcel);
        await db.SaveChangesAsync();
        return parcel;
    }
}
