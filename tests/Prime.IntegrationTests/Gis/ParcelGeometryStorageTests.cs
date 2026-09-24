using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NetTopologySuite;
using NetTopologySuite.Geometries;
using Prime.Domain.Entities;
using Prime.Domain.Entities.Reference;
using Prime.Infrastructure.GIS;
using Prime.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Prime.IntegrationTests.Gis;

/// <summary>
/// Phase 7 step 1 (docs/GIS.md §2): the typed geometry column is enforced
/// by PostGIS itself, and measured areas are real square metres — never
/// raw WGS84 degrees. Runs against the real local database inside a
/// transaction that is never committed.
/// </summary>
public class ParcelGeometryStorageTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly GeometryFactory Wgs84 = NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);

    // 0.001° × 0.001° at ~14.5°N: roughly 107.8 m × 110.6 m ≈ 11,925 m².
    private static Polygon Square() => Wgs84.CreatePolygon([
        new Coordinate(121.0, 14.5),
        new Coordinate(121.001, 14.5),
        new Coordinate(121.001, 14.501),
        new Coordinate(121.0, 14.501),
        new Coordinate(121.0, 14.5),
    ]);

    [Fact]
    public async Task Database_RejectsNonArealGeometry()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PrimeDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync();

        db.Parcels.Add(await NewParcelAsync(db, Wgs84.CreatePoint(new Coordinate(121.0, 14.5))));

        var ex = await Should.ThrowAsync<DbUpdateException>(() => db.SaveChangesAsync());
        ex.InnerException!.Message.ShouldContain("does not match column type");
    }

    [Fact]
    public async Task Database_StoresBarePolygon_AsMultiPolygon()
    {
        // PostGIS 3.x promotes a single POLYGON into a MULTIPOLYGON column
        // on write (verified against 3.6.2). ParcelService normalizes before
        // saving anyway, so correctness does not depend on this behavior.
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PrimeDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync();

        var parcel = await NewParcelAsync(db, Square());
        db.Parcels.Add(parcel);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var reloaded = await db.Parcels.SingleAsync(p => p.Id == parcel.Id);
        reloaded.Geometry.ShouldBeOfType<MultiPolygon>();
        reloaded.Geometry!.SRID.ShouldBe(4326);
    }

    [Fact]
    public async Task Database_RejectsWrongSrid()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PrimeDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync();

        var prs92 = NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4683);
        var geometry = prs92.CreateMultiPolygon([prs92.CreatePolygon(Square().Coordinates)]);
        db.Parcels.Add(await NewParcelAsync(db, geometry));

        var ex = await Should.ThrowAsync<DbUpdateException>(() => db.SaveChangesAsync());
        ex.InnerException!.Message.ShouldContain("SRID");
    }

    [Fact]
    public async Task MeasuredArea_IsSquareMetres_GeodesicAndProjectedAgree()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PrimeDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync();

        var parcel = await NewParcelAsync(db, Wgs84.CreateMultiPolygon([Square()]));
        db.Parcels.Add(parcel);
        await db.SaveChangesAsync();

        var geodesic = new GeometryMeasurementService(db, Options.Create(new GisOptions()));
        // EPSG:3123 = PRS92 / Philippines zone 3 (central meridian 121°E),
        // used here only as a realistic projected CRS for the test point.
        var projected = new GeometryMeasurementService(db, Options.Create(new GisOptions { MeasurementSrid = 3123 }));

        var geodesicArea = (await geodesic.GetParcelAreasAsync([parcel.Id]))[parcel.Id];
        var projectedArea = (await projected.GetParcelAreasAsync([parcel.Id]))[parcel.Id];

        geodesic.AreaBasis.ShouldBe("GEODESIC_WGS84");
        projected.AreaBasis.ShouldBe("PROJECTED_EPSG_3123");
        geodesicArea.ShouldBeInRange(11_800m, 12_050m);
        Math.Abs(projectedArea - geodesicArea).ShouldBeLessThan(geodesicArea * 0.005m);
    }

    [Fact]
    public async Task MeasuredArea_OmitsParcelsWithoutGeometry()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PrimeDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync();

        var parcel = await NewParcelAsync(db, null);
        db.Parcels.Add(parcel);
        await db.SaveChangesAsync();

        var areas = await new GeometryMeasurementService(db, Options.Create(new GisOptions())).GetParcelAreasAsync([parcel.Id]);

        areas.ShouldBeEmpty();
    }

    private static async Task<Parcel> NewParcelAsync(PrimeDbContext db, Geometry? geometry)
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
        db.Properties.Add(property);
        await db.SaveChangesAsync();

        return new Parcel { Property = property, Barangay = barangay, Geometry = geometry };
    }
}
