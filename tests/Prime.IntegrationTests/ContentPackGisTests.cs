using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Prime.Application.Common;
using Prime.Application.Features.ContentPacks;
using Prime.Domain.Entities.Audit;
using Prime.Domain.Entities.Identity;
using Prime.Infrastructure.Identity;
using Prime.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Prime.IntegrationTests;

/// <summary>
/// Step C5 of the LGU content pack (docs/analysis/lgu-content-pack.md): map
/// layers travel in a pack through the reference-layer import. Barangays the
/// pack adds count as existing in the preview; unchanged shapes are skipped,
/// so a second import is a no-op; history stays append-only. Everything is
/// rolled back.
/// </summary>
public class ContentPackGisTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private sealed record Ctx(IContentPackService Service, PrimeDbContext Db);

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Prime.slnx")))
        {
            dir = dir.Parent;
        }
        return dir?.FullName ?? throw new InvalidOperationException("Repository root (Prime.slnx) not found.");
    }

    private async Task<(Ctx C, IAsyncDisposable Scope)> BeginAsync(string root)
    {
        var host = factory.WithWebHostBuilder(b => b.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?> { ["ContentPacks:RootPath"] = root })));
        var scope = host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PrimeDbContext>();
        var transaction = await db.Database.BeginTransactionAsync();
        await db.Provinces.Where(x => x.PinIndexNumber == "998").ExecuteUpdateAsync(s => s.SetProperty(x => x.PinIndexNumber, (string?)null));
        var importer = new AppUser { SupabaseUserId = Guid.NewGuid(), DisplayName = "DEMO Importer", Email = $"demo-{Guid.NewGuid():N}@example.invalid" };
        db.AppUsers.Add(importer);
        await db.SaveChangesAsync();
        scope.ServiceProvider.GetRequiredService<CurrentUserService>().AppUserId = importer.Id;
        return (new Ctx(scope.ServiceProvider.GetRequiredService<IContentPackService>(), db), new Disposer(transaction, scope, host));
    }

    private sealed class Disposer(IAsyncDisposable transaction, IAsyncDisposable scope, IAsyncDisposable host) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            await transaction.DisposeAsync();
            await scope.DisposeAsync();
            await host.DisposeAsync();
        }
    }

    private static void WritePack(string root, string pack, IReadOnlyDictionary<string, string> files)
    {
        foreach (var (path, content) in files)
        {
            var full = Path.Combine(root, pack, path.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllText(full, content);
        }
    }

    private static string Square(string psgc, double lon, double lat) => $$"""
        { "type": "FeatureCollection", "features": [ { "type": "Feature", "properties": { "psgcCode": "{{psgc}}" },
          "geometry": { "type": "Polygon", "coordinates": [[[{{lon}}, {{lat}}], [{{lon + 0.01}}, {{lat}}], [{{lon + 0.01}}, {{lat + 0.01}}], [{{lon}}, {{lat + 0.01}}], [{{lon}}, {{lat}}]]] } } ] }
        """;

    private static string LayerManifest(string pack, string effectiveDate) => $$"""
        { "schemaVersion": 1, "pack": "{{pack}}", "version": "T-{{effectiveDate}}", "files": [
          { "kind": "provinces", "path": "p.csv", "source": "DEMO" },
          { "kind": "municipalities", "path": "m.csv", "source": "DEMO" },
          { "kind": "barangays", "path": "b.csv", "source": "DEMO" },
          { "kind": "gis-layer", "layer": "barangays", "effectiveDate": "{{effectiveDate}}", "path": "gis/b.geojson", "source": "DEMO shapes" }
        ] }
        """;

    private static IEnumerable<string> Codes(ContentPackPreviewDto p) => p.Issues.Concat(p.Files.SelectMany(f => f.Issues)).Select(i => i.Code);

    [Fact]
    public async Task DemoPack_ImportsItsLayer_WithProvenance_AndASecondImportChangesNothing()
    {
        var (c, scope) = await BeginAsync(Path.Combine(RepoRoot(), "samples"));
        await using var _ = scope;

        var preview = (await c.Service.PreviewAsync("content-demo")).Value;
        preview.CanImport.ShouldBeTrue();
        var record = (await c.Service.ImportAsync("content-demo", new(preview.Fingerprint))).Value.Import.ShouldNotBeNull();

        var barangay = await c.Db.Barangays.SingleAsync(x => x.PsgcCode == "9900100001");
        var boundary = await c.Db.BarangayBoundaries.SingleAsync(x => x.BarangayId == barangay.Id);
        (boundary.EffectiveDate, boundary.EndDate, boundary.Source).ShouldBe((new DateOnly(2026, 1, 1), (DateOnly?)null, "DEMO shapes in open sea, not a real boundary"));

        var items = (await c.Service.ListImportItemsAsync(record.Id, new PagedRequest { PageSize = 100 })).Value.Items
            .Where(i => i.EntityType == "BarangayBoundary").ToList();
        items.Select(i => (i.Key, i.Line)).ShouldBe([("9900100001", 1), ("9900100002", 2)]);
        items.ShouldAllBe(i => i.FilePath == "gis/barangays.geojson" && i.Source == "DEMO shapes in open sea, not a real boundary");
        items[0].EntityId.ShouldBe(boundary.Id);
        record.Files.Single(f => f.Kind == "gis-layer").Layer.ShouldBe("barangays");

        // The pack's reason prefixes the layer import's own.
        (await c.Db.Set<AuditLog>().AnyAsync(a => a.RecordId == boundary.Id && a.Reason!.StartsWith("Content pack content-demo DEMO-1: GIS import"))).ShouldBeTrue();

        var again = (await c.Service.PreviewAsync("content-demo")).Value;
        var layer = again.Files.Single(f => f.Kind == "gis-layer");
        (layer.New, layer.Changed, layer.Unchanged).ShouldBe((0, 0, 2));
        (await c.Service.ImportAsync("content-demo", new(again.Fingerprint))).Value.Applied.ShouldBeFalse();
    }

    [Fact]
    public async Task ChangedShape_NeedsALaterDate_AndSupersedesTheVersionInForce()
    {
        var tag = Random.Shared.Next(10_000_000, 99_999_999).ToString(System.Globalization.CultureInfo.InvariantCulture);
        var (prov, mun, brgy) = ($"96{tag}", $"96{tag[..6]}01", $"96{tag[..6]}11");
        var root = Path.Combine(Path.GetTempPath(), $"prime-cp-{Guid.NewGuid():N}");
        WritePack(root, "layers", new Dictionary<string, string>
        {
            ["manifest.json"] = LayerManifest("layers", "2026-01-01"),
            ["p.csv"] = $"psgc_code,name\n{prov},DEMO GIS Province\n",
            ["m.csv"] = $"psgc_code,province_psgc,name\n{mun},{prov},DEMO GIS Town\n",
            ["b.csv"] = $"psgc_code,municipality_psgc,name\n{brgy},{mun},DEMO GIS Barangay\n",
            ["gis/b.geojson"] = Square(brgy, 127.6, 12.0),
        });
        var (c, scope) = await BeginAsync(root);
        await using var _ = scope;

        var first = (await c.Service.PreviewAsync("layers")).Value;
        (await c.Service.ImportAsync("layers", new(first.Fingerprint))).Value.Applied.ShouldBeTrue();

        // Same date, different shape: history is append-only, so it is refused.
        WritePack(root, "layers", new Dictionary<string, string> { ["gis/b.geojson"] = Square(brgy, 127.7, 12.0) });
        var sameDate = (await c.Service.PreviewAsync("layers")).Value;
        sameDate.CanImport.ShouldBeFalse();
        sameDate.Files.Single(f => f.Kind == "gis-layer").Issues.ShouldContain(i => i.Code == "VERSION_NOT_AFTER_EXISTING" && i.Line == 1);

        // A later date: the new shape supersedes the version in force.
        WritePack(root, "layers", new Dictionary<string, string> { ["manifest.json"] = LayerManifest("layers", "2026-07-01") });
        var later = (await c.Service.PreviewAsync("layers")).Value;
        later.CanImport.ShouldBeTrue();
        var layer = later.Files.Single(f => f.Kind == "gis-layer");
        (layer.New, layer.Changed, layer.Unchanged).ShouldBe((0, 1, 0));
        layer.Changes.Single().Action.ShouldBe(ContentChangeAction.Changed);
        (await c.Service.ImportAsync("layers", new(later.Fingerprint))).Value.Applied.ShouldBeTrue();

        var barangayId = (await c.Db.Barangays.SingleAsync(x => x.PsgcCode == brgy)).Id;
        var versions = await c.Db.BarangayBoundaries.AsNoTracking().Where(x => x.BarangayId == barangayId).OrderBy(x => x.EffectiveDate).ToListAsync();
        versions.Select(v => (v.EffectiveDate, v.EndDate)).ShouldBe([
            (new DateOnly(2026, 1, 1), (DateOnly?)new DateOnly(2026, 7, 1)),
            (new DateOnly(2026, 7, 1), (DateOnly?)null),
        ]);
        versions[1].Geometry.EnvelopeInternal.MinX.ShouldBe(127.7, 1e-9);
        Directory.Delete(root, recursive: true);
    }

    [Fact]
    public async Task LayerEntries_NeedALayer_ADate_ASource_ValidJson_AndKnownKeys()
    {
        var root = Path.Combine(Path.GetTempPath(), $"prime-cp-{Guid.NewGuid():N}");
        WritePack(root, "badlayers", new Dictionary<string, string>
        {
            ["manifest.json"] = """
                { "schemaVersion": 1, "pack": "badlayers", "version": "T1", "files": [
                  { "kind": "gis-layer", "path": "a.geojson", "effectiveDate": "2026-01-01", "source": "DEMO" },
                  { "kind": "gis-layer", "layer": "zones", "path": "b.geojson", "effectiveDate": "01/01/2026", "source": "DEMO" },
                  { "kind": "gis-layer", "layer": "roads", "path": "c.geojson", "effectiveDate": "2026-01-01" },
                  { "kind": "gis-layer", "layer": "sections", "path": "d.geojson", "effectiveDate": "2026-01-01", "source": "DEMO" },
                  { "kind": "gis-layer", "layer": "barangays", "path": "e.geojson", "effectiveDate": "2026-01-01", "source": "DEMO" },
                  { "kind": "gis-layer", "layer": "barangays", "path": "f.geojson", "effectiveDate": "2026-01-01", "source": "DEMO" }
                ] }
                """,
            ["d.geojson"] = "{ not json",
            ["e.geojson"] = Square("9599999999", 127.8, 12.0),
            ["f.geojson"] = Square("9599999998", 127.8, 12.0),
        });
        var (c, scope) = await BeginAsync(root);
        await using var _ = scope;

        var preview = (await c.Service.PreviewAsync("badlayers")).Value;
        var codes = Codes(preview).ToList();

        preview.CanImport.ShouldBeFalse();
        codes.ShouldContain("MANIFEST_LAYER");
        codes.ShouldContain("MANIFEST_EFFECTIVE_DATE");
        codes.ShouldContain("SOURCE_MISSING");
        codes.ShouldContain("GEOJSON_INVALID");
        codes.ShouldContain("MANIFEST_DUPLICATE_KIND"); // a second barangays layer
        preview.Files.Single(f => f.Path == "e.geojson").Issues
            .ShouldContain(i => i.Code == "BARANGAY_NOT_FOUND" && i.Line == 1 && i.Message.StartsWith("Feature 1:"));
        Directory.Delete(root, recursive: true);
    }
}
