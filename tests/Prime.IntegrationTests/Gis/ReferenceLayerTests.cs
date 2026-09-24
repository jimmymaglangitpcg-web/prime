using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NetTopologySuite;
using NetTopologySuite.Geometries;
using Prime.Application.Features.Gis.ReferenceLayers;
using Prime.Domain.Entities.Audit;
using Prime.Domain.Entities.Gis;
using Prime.Domain.Entities.Reference;
using Prime.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Prime.IntegrationTests.Gis;

/// <summary>
/// Phase 7 step 4a: effective-dated reference layers (barangay/zone/road)
/// and their GeoJSON import. Real services against real PostGIS inside a
/// never-committed transaction (the import joins the ambient transaction).
/// </summary>
public class ReferenceLayerTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private sealed record Ctx(IServiceScope Scope, PrimeDbContext Db, IReferenceLayerService Layers, double Lon, double Lat) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            if (Db.Database.CurrentTransaction is { } tx)
            {
                await tx.DisposeAsync();
            }
            Scope.Dispose();
        }
    }

    private async Task<Ctx> BeginAsync()
    {
        var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PrimeDbContext>();
        await db.Database.BeginTransactionAsync();
        return new Ctx(scope, db, scope.ServiceProvider.GetRequiredService<IReferenceLayerService>(),
            120.0 + Random.Shared.NextDouble() * 2, 10.0 + Random.Shared.NextDouble() * 5);
    }

    private static string Square(double lon, double lat, double size = 0.01) =>
        FormattableString.Invariant($"[[[{lon},{lat}],[{lon + size},{lat}],[{lon + size},{lat + size}],[{lon},{lat + size}],[{lon},{lat}]]]");

    private static JsonElement Collection(params string[] features) =>
        JsonDocument.Parse($$"""{"type":"FeatureCollection","features":[{{string.Join(",", features)}}]}""").RootElement.Clone();

    private static string PolygonFeature(string keyName, string key, string coordinates) =>
        $$$"""{"type":"Feature","properties":{"{{{keyName}}}":"{{{key}}}"},"geometry":{"type":"Polygon","coordinates":{{{coordinates}}}}}""";

    private static string Bbox(double lon, double lat, double size = 0.05) =>
        FormattableString.Invariant($"{lon - size},{lat - size},{lon + size},{lat + size}");

    private static async Task<Barangay> SeedBarangayAsync(PrimeDbContext db)
    {
        var province = new Province { PsgcCode = $"P{Guid.NewGuid():N}"[..10], Name = "Demo Province" };
        var municipality = new Municipality { Province = province, PsgcCode = $"M{Guid.NewGuid():N}"[..10], Name = "Demo Municipality" };
        var barangay = new Barangay { Municipality = municipality, PsgcCode = $"B{Guid.NewGuid():N}"[..10], Name = "DEMO Barangay" };
        db.Barangays.Add(barangay);
        await db.SaveChangesAsync();
        return barangay;
    }

    private static ImportReferenceLayerRequest Request(JsonElement collection, DateOnly? effective = null) =>
        new(effective ?? new DateOnly(2025, 1, 1), "DEMO test boundaries", "DEMO-REF-1", collection);

    [Fact]
    public async Task DryRun_ValidatesAndCounts_ButWritesNothing()
    {
        await using var ctx = await BeginAsync();
        var barangay = await SeedBarangayAsync(ctx.Db);

        var result = await ctx.Layers.ImportAsync(ReferenceLayer.Barangays,
            Request(Collection(PolygonFeature("psgcCode", barangay.PsgcCode, Square(ctx.Lon, ctx.Lat)))), dryRun: true);

        result.Value.Errors.ShouldBeEmpty();
        result.Value.Committed.ShouldBeFalse();
        result.Value.NewFeatures.ShouldBe(1);
        (await ctx.Db.BarangayBoundaries.CountAsync(b => b.BarangayId == barangay.Id)).ShouldBe(0);
    }

    [Fact]
    public async Task Commit_ThenQuery_ReturnsFeatureWithProvenance_AndAuditsBatch()
    {
        await using var ctx = await BeginAsync();
        var barangay = await SeedBarangayAsync(ctx.Db);

        var import = await ctx.Layers.ImportAsync(ReferenceLayer.Barangays,
            Request(Collection(PolygonFeature("psgcCode", barangay.PsgcCode, Square(ctx.Lon, ctx.Lat)))), dryRun: false);

        import.Value.Committed.ShouldBeTrue();
        import.Value.ImportBatchId.ShouldNotBeNull();

        var layer = await ctx.Layers.GetFeaturesAsync(ReferenceLayer.Barangays, Bbox(ctx.Lon, ctx.Lat), new DateOnly(2025, 6, 1), null);
        var feature = layer.Value.Features.ShouldHaveSingleItem();
        feature.Properties.Key.ShouldBe(barangay.PsgcCode);
        feature.Properties.Name.ShouldBe("DEMO Barangay");
        feature.Properties.Source.ShouldBe("DEMO test boundaries");
        feature.Properties.SourceReference.ShouldBe("DEMO-REF-1");
        feature.Geometry.GetProperty("type").GetString().ShouldBe("MultiPolygon");

        var audit = await ctx.Db.Set<AuditLog>().SingleAsync(a => a.RecordId == feature.Id);
        audit.Module.ShouldBe("Gis");
        audit.Reason!.ShouldContain(import.Value.ImportBatchId!.Value.ToString());
    }

    [Fact]
    public async Task NewVersion_ClosesPrevious_AndAsOfQueriesSeeTheRightOne()
    {
        await using var ctx = await BeginAsync();
        var barangay = await SeedBarangayAsync(ctx.Db);

        await ctx.Layers.ImportAsync(ReferenceLayer.Barangays,
            Request(Collection(PolygonFeature("psgcCode", barangay.PsgcCode, Square(ctx.Lon, ctx.Lat, 0.01))), new DateOnly(2024, 1, 1)), dryRun: false);
        var second = await ctx.Layers.ImportAsync(ReferenceLayer.Barangays,
            Request(Collection(PolygonFeature("psgcCode", barangay.PsgcCode, Square(ctx.Lon, ctx.Lat, 0.02))), new DateOnly(2025, 1, 1)), dryRun: false);

        second.Value.Committed.ShouldBeTrue(string.Join("; ", second.Value.Errors.Select(e => e.Message)));
        second.Value.SupersededVersions.ShouldBe(1);
        second.Value.NewFeatures.ShouldBe(0);

        var versions = await ctx.Db.BarangayBoundaries.Where(b => b.BarangayId == barangay.Id).OrderBy(b => b.EffectiveDate).ToListAsync();
        versions.Count.ShouldBe(2);
        versions[0].EndDate.ShouldBe(new DateOnly(2025, 1, 1));
        versions[1].EndDate.ShouldBeNull();

        var in2024 = await ctx.Layers.GetFeaturesAsync(ReferenceLayer.Barangays, Bbox(ctx.Lon, ctx.Lat), new DateOnly(2024, 12, 31), null);
        var onChangeDay = await ctx.Layers.GetFeaturesAsync(ReferenceLayer.Barangays, Bbox(ctx.Lon, ctx.Lat), new DateOnly(2025, 1, 1), null);
        var before = await ctx.Layers.GetFeaturesAsync(ReferenceLayer.Barangays, Bbox(ctx.Lon, ctx.Lat), new DateOnly(2023, 12, 31), null);

        in2024.Value.Features.ShouldHaveSingleItem().Id.ShouldBe(versions[0].Id);
        onChangeDay.Value.Features.ShouldHaveSingleItem().Id.ShouldBe(versions[1].Id); // half-open interval
        before.Value.Features.ShouldBeEmpty();
    }

    [Fact]
    public async Task VersionNotAfterExisting_IsRejected_AndNothingChanges()
    {
        await using var ctx = await BeginAsync();
        var barangay = await SeedBarangayAsync(ctx.Db);
        await ctx.Layers.ImportAsync(ReferenceLayer.Barangays,
            Request(Collection(PolygonFeature("psgcCode", barangay.PsgcCode, Square(ctx.Lon, ctx.Lat))), new DateOnly(2025, 1, 1)), dryRun: false);

        var sameDay = await ctx.Layers.ImportAsync(ReferenceLayer.Barangays,
            Request(Collection(PolygonFeature("psgcCode", barangay.PsgcCode, Square(ctx.Lon, ctx.Lat, 0.02))), new DateOnly(2025, 1, 1)), dryRun: false);

        sameDay.Value.Committed.ShouldBeFalse();
        sameDay.Value.Errors.ShouldHaveSingleItem().Code.ShouldBe("VERSION_NOT_AFTER_EXISTING");
        ctx.Db.ChangeTracker.Clear();
        var versions = await ctx.Db.BarangayBoundaries.Where(b => b.BarangayId == barangay.Id).ToListAsync();
        versions.ShouldHaveSingleItem().EndDate.ShouldBeNull();
    }

    [Fact]
    public async Task InvalidFeatures_AreAllReported_AndValidOnesAreNotPartiallyImported()
    {
        await using var ctx = await BeginAsync();
        var barangay = await SeedBarangayAsync(ctx.Db);
        var sq = Square(ctx.Lon, ctx.Lat);

        var result = await ctx.Layers.ImportAsync(ReferenceLayer.Barangays, Request(Collection(
            PolygonFeature("psgcCode", barangay.PsgcCode, sq),                                   // 0 valid
            PolygonFeature("psgcCode", barangay.PsgcCode, sq),                                   // 1 duplicate key
            PolygonFeature("psgcCode", "NO-SUCH-PSGC", sq),                                      // 2 unknown barangay
            PolygonFeature("wrongKey", "x", sq),                                                 // 3 missing key
            """{"type":"Feature","properties":{"psgcCode":"P4"},"geometry":{"type":"Point","coordinates":[121,14]}}""", // 4 wrong type
            PolygonFeature("psgcCode", "P5", "[[[500000,1600000],[500100,1600000],[500100,1600100],[500000,1600000]]]"), // 5 projected metres
            PolygonFeature("psgcCode", "P6", FormattableString.Invariant($"[[[{ctx.Lon},{ctx.Lat}],[{ctx.Lon + 0.01},{ctx.Lat + 0.01}],[{ctx.Lon + 0.01},{ctx.Lat}],[{ctx.Lon},{ctx.Lat + 0.01}],[{ctx.Lon},{ctx.Lat}]]]")))), // 6 bow-tie
            dryRun: false);

        result.Value.Committed.ShouldBeFalse();
        result.Value.FeatureCount.ShouldBe(7);
        var byIndex = result.Value.Errors.ToDictionary(e => e.FeatureIndex!.Value, e => e.Code);
        byIndex.ShouldBe(new Dictionary<int, string>
        {
            [1] = "DUPLICATE_KEY",
            [2] = "BARANGAY_NOT_FOUND",
            [3] = "KEY_REQUIRED",
            [4] = "INVALID_GEOMETRY_TYPE",
            [5] = "COORDINATES_OUT_OF_RANGE",
            [6] = "INVALID_GEOMETRY",
        }, ignoreOrder: true);
        (await ctx.Db.BarangayBoundaries.CountAsync(b => b.BarangayId == barangay.Id)).ShouldBe(0);
    }

    [Theory]
    [InlineData("""{"type":"FeatureCollection","crs":{"type":"name","properties":{"name":"urn:ogc:def:crs:EPSG::3123"}},"features":[]}""", "UNSUPPORTED_CRS")]
    [InlineData("""{"type":"FeatureCollection","features":[]}""", "NO_FEATURES")]
    [InlineData("""{"type":"Feature"}""", "INVALID_GEOJSON")]
    public async Task CollectionLevelProblems_AreReported(string json, string expectedCode)
    {
        await using var ctx = await BeginAsync();

        var result = await ctx.Layers.ImportAsync(ReferenceLayer.Zones, Request(JsonDocument.Parse(json).RootElement.Clone()), dryRun: true);

        result.Value.Errors.ShouldContain(e => e.Code == expectedCode && e.FeatureIndex == null);
    }

    [Fact]
    public async Task MissingSourceOrDate_IsReported()
    {
        await using var ctx = await BeginAsync();

        var result = await ctx.Layers.ImportAsync(ReferenceLayer.Zones,
            new ImportReferenceLayerRequest(default, " ", null, Collection()), dryRun: true);

        result.Value.Errors.Select(e => e.Code).ShouldContain("EFFECTIVE_DATE_REQUIRED");
        result.Value.Errors.Select(e => e.Code).ShouldContain("SOURCE_REQUIRED");
    }

    [Fact]
    public async Task Roads_AcceptLineStrings_ResolveRoadType_AndRejectUnknownType()
    {
        await using var ctx = await BeginAsync();
        var roadType = new RoadType { Code = $"DEMO_RT_{Guid.NewGuid():N}"[..20], Name = "DEMO Road Type" };
        ctx.Db.RoadTypes.Add(roadType);
        await ctx.Db.SaveChangesAsync();
        string Road(string code, string? typeCode) => FormattableString.Invariant(
            $$$"""{"type":"Feature","properties":{"code":"{{{code}}}","name":"DEMO Road {{{code}}}"{{{(typeCode is null ? "" : $",\"roadTypeCode\":\"{typeCode}\"")}}}},"geometry":{"type":"LineString","coordinates":[[{{{ctx.Lon}}},{{{ctx.Lat}}}],[{{{ctx.Lon + 0.01}}},{{{ctx.Lat + 0.01}}}]]}}""");
        var codeA = $"DEMO-RD-{Guid.NewGuid():N}"[..20];
        var codeB = $"DEMO-RD-{Guid.NewGuid():N}"[..20];

        var bad = await ctx.Layers.ImportAsync(ReferenceLayer.Roads, Request(Collection(Road(codeA, "NOPE"))), dryRun: true);
        bad.Value.Errors.ShouldHaveSingleItem().Code.ShouldBe("ROAD_TYPE_NOT_FOUND");

        var good = await ctx.Layers.ImportAsync(ReferenceLayer.Roads, Request(Collection(Road(codeA, roadType.Code), Road(codeB, null))), dryRun: false);
        good.Value.Committed.ShouldBeTrue();

        var stored = await ctx.Db.RoadSegments.SingleAsync(r => r.Code == codeA);
        stored.RoadTypeId.ShouldBe(roadType.Id);
        stored.Geometry.ShouldBeOfType<MultiLineString>();
        var layer = await ctx.Layers.GetFeaturesAsync(ReferenceLayer.Roads, Bbox(ctx.Lon, ctx.Lat), new DateOnly(2025, 2, 1), null);
        layer.Value.Features.Select(f => f.Properties.Key).OrderBy(k => k).ShouldBe(new[] { codeA, codeB }.OrderBy(k => k));
    }

    [Fact]
    public async Task Database_AllowsOnlyOneCurrentVersion_AndValidInterval()
    {
        await using var ctx = await BeginAsync();
        var barangay = await SeedBarangayAsync(ctx.Db);
        var wgs84 = NtsGeometryServices.Instance.CreateGeometryFactory(4326);
        MultiPolygon Shape() => wgs84.CreateMultiPolygon([wgs84.CreatePolygon([
            new Coordinate(ctx.Lon, ctx.Lat), new Coordinate(ctx.Lon + 0.01, ctx.Lat), new Coordinate(ctx.Lon, ctx.Lat + 0.01), new Coordinate(ctx.Lon, ctx.Lat)])]);
        BarangayBoundary Version(DateOnly from, DateOnly? to) => new()
        {
            BarangayId = barangay.Id, Geometry = Shape(), EffectiveDate = from, EndDate = to, Source = "DEMO", ImportBatchId = Guid.NewGuid(),
        };

        await ctx.Db.Database.ExecuteSqlRawAsync("SAVEPOINT before_checks");
        ctx.Db.BarangayBoundaries.AddRange(Version(new DateOnly(2024, 1, 1), null), Version(new DateOnly(2025, 1, 1), null));
        var twoCurrent = await Should.ThrowAsync<DbUpdateException>(() => ctx.Db.SaveChangesAsync());
        twoCurrent.InnerException!.Message.ShouldContain("UX_BarangayBoundaries_Current");

        ctx.Db.ChangeTracker.Clear();
        await ctx.Db.Database.ExecuteSqlRawAsync("ROLLBACK TO SAVEPOINT before_checks");
        ctx.Db.BarangayBoundaries.Add(Version(new DateOnly(2025, 1, 1), new DateOnly(2025, 1, 1)));
        var badInterval = await Should.ThrowAsync<DbUpdateException>(() => ctx.Db.SaveChangesAsync());
        badInterval.InnerException!.Message.ShouldContain("CK_BarangayBoundaries_EndDate");
    }

    [Fact]
    public async Task Http_UnknownLayer_Is404_AndFailedCommitIs422WithReport()
    {
        var client = factory.CreateClient();

        var unknown = await client.GetAsync("/api/gis/layers/rivers?bbox=121,14,121.1,14.1");
        unknown.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await unknown.Content.ReadAsStringAsync()).ShouldContain("LAYER_NOT_FOUND");

        // Unknown PSGC code → validation error → nothing written, 422.
        var body = new ImportReferenceLayerRequest(new DateOnly(2025, 1, 1), "DEMO", null,
            Collection(PolygonFeature("psgcCode", "NO-SUCH-PSGC-HTTP", Square(121, 14))));
        var commit = await client.PostAsJsonAsync("/api/gis/layers/barangays/import?dryRun=false", body);
        commit.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        using var report = JsonDocument.Parse(await commit.Content.ReadAsStringAsync());
        report.RootElement.GetProperty("committed").GetBoolean().ShouldBeFalse();
        report.RootElement.GetProperty("errors")[0].GetProperty("code").GetString().ShouldBe("BARANGAY_NOT_FOUND");

        var dryRunByDefault = await client.PostAsJsonAsync("/api/gis/layers/barangays/import", body);
        dryRunByDefault.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var dryReport = JsonDocument.Parse(await dryRunByDefault.Content.ReadAsStringAsync());
        dryReport.RootElement.GetProperty("dryRun").GetBoolean().ShouldBeTrue();
    }
}
