using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Prime.Application.Features.ContentPacks;
using Prime.Domain.Entities;
using Prime.Domain.Entities.Reference;
using Prime.Domain.Enums;
using Prime.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Prime.IntegrationTests;

/// <summary>
/// Step C1 of the LGU content pack (docs/analysis/lgu-content-pack.md): the dry
/// run validates a pack and compares it with the database without writing.
/// Uses the committed DEMO pack (samples/content-demo) and throw-away packs in
/// the temp folder; database rows made here are rolled back.
/// </summary>
public class ContentPackPreviewTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Prime.slnx")))
        {
            dir = dir.Parent;
        }
        return dir?.FullName ?? throw new InvalidOperationException("Repository root (Prime.slnx) not found.");
    }

    private async Task<(IContentPackService Service, PrimeDbContext Db, IAsyncDisposable Scope)> BeginAsync(string root)
    {
        var host = factory.WithWebHostBuilder(b => b.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?> { ["ContentPacks:RootPath"] = root })));
        var scope = host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PrimeDbContext>();
        var transaction = await db.Database.BeginTransactionAsync();
        return (scope.ServiceProvider.GetRequiredService<IContentPackService>(), db, new Disposer(transaction, scope, host));
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

    /// <summary>A throw-away pack folder under a fresh temp root.</summary>
    private static string TempPack(string pack, IReadOnlyDictionary<string, string> files)
    {
        var root = Path.Combine(Path.GetTempPath(), $"prime-cp-{Guid.NewGuid():N}");
        foreach (var (path, content) in files)
        {
            var full = Path.Combine(root, pack, path.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllText(full, content);
        }
        return root;
    }

    private static IEnumerable<string> Codes(ContentPackPreviewDto p) => p.Issues.Concat(p.Files.SelectMany(f => f.Issues)).Select(i => i.Code);

    [Fact]
    public async Task DemoPack_PreviewsClean_AndWritesNothing()
    {
        var (service, db, scope) = await BeginAsync(Path.Combine(RepoRoot(), "samples"));
        await using var _ = scope;
        await db.Provinces.Where(x => x.PinIndexNumber == "998").ExecuteUpdateAsync(s => s.SetProperty(x => x.PinIndexNumber, (string?)null));
        var before = (await db.Provinces.CountAsync(), await db.Barangays.CountAsync(), await db.Classifications.CountAsync());

        (await service.ListAsync()).Value.ShouldContain(p => p.Pack == "content-demo" && p.HasManifest);
        var preview = (await service.PreviewAsync("content-demo")).Value;

        preview.Issues.ShouldBeEmpty();
        preview.Files.SelectMany(f => f.Issues).Where(i => i.Severity == ContentIssueSeverity.Error).ShouldBeEmpty();
        preview.CanImport.ShouldBeTrue();
        preview.Version.ShouldBe("DEMO-1");
        preview.ManifestSha256.ShouldNotBeNull().Length.ShouldBe(64);
        var files = preview.Files.ToDictionary(f => f.Lookup ?? f.Kind);
        files["provinces"].New.ShouldBe(1);
        files["municipalities"].New.ShouldBe(2);
        files["barangays"].New.ShouldBe(4);
        files["barangays"].Changes.ShouldContain(c => c.Name == "DEMO Poblacion, Town A");
        files["classifications"].New.ShouldBe(2);
        files["structural-materials"].New.ShouldBe(2);
        foreach (var kind in new[] { "transaction-types", "numbering-schemes", "approval-chains", "forms" })
        {
            (files[kind].New, files[kind].Rows).ShouldBe((1, 1)); // one Draft version each, from one catalogue item
        }
        files["forms"].Changes.Single().Fields.ShouldContain(f => f.Field == "template" && f.From == null);
        files.Values.ShouldAllBe(f => f.Sha256 != null && f.Supported);

        (await db.Provinces.CountAsync(), await db.Barangays.CountAsync(), await db.Classifications.CountAsync()).ShouldBe(before);
    }

    [Fact]
    public async Task BrokenPack_ReportsEveryProblem_AndCannotImport()
    {
        const string manifest = """
            { "schemaVersion": 1, "pack": "broken", "version": "T1", "files": [
              { "kind": "provinces", "path": "provinces.csv", "source": "DEMO" },
              { "kind": "municipalities", "path": "municipalities.csv", "source": "DEMO" },
              { "kind": "barangays", "path": "barangays.csv" },
              { "kind": "lookup", "lookup": "classifications", "path": "classes.csv", "source": "DEMO" },
              { "kind": "lookup", "lookup": "no-such-lookup", "path": "x.csv", "source": "DEMO" },
              { "kind": "lookup", "lookup": "structural-materials", "path": "materials.csv", "source": "DEMO" },
              { "kind": "gis-layer", "path": "gis/layer.geojson", "source": "DEMO" },
              { "kind": "provinces", "path": "../escape.csv", "source": "DEMO" },
              { "kind": "wizardry", "path": "w.csv", "source": "DEMO" },
              { "kind": "lookup", "lookup": "zones", "path": "missing.csv", "source": "DEMO" }
            ] }
            """;
        var root = TempPack("broken", new Dictionary<string, string>
        {
            ["manifest.json"] = manifest,
            ["provinces.csv"] = "psgc_code,name,index_number,colour\n9700000000,DEMO P,97,blue\n9700000001,DEMO Q,971,\n9700000001,DEMO Q again,,\n",
            ["municipalities.csv"] = "psgc_code,province_psgc,name,is_city,index_number\n9700100000,9799999999,DEMO Orphan,false,01\n9700100001,9700000001,DEMO Town,maybe,02\n9700100002,9700000001,DEMO T1,false,05\n9700100003,9700000001,DEMO T2,false,05\n",
            ["barangays.csv"] = "psgc_code,municipality_psgc,name,index_number\n9700100201,9700100002,DEMO B,001\n9700100202,9700100002,DEMO C,0001\n",
            ["classes.csv"] = "code,name,sort_order\nDEMO-X,DEMO X,first\nDEMO-Y,,1\nDEMO-Z,DEMO Z,2\nDEMO-Z,DEMO Z twice,3\n",
            ["materials.csv"] = "code,name,part_code\nDEMO-M1,DEMO M1,DEMO-NO-PART\n",
            ["gis/layer.geojson"] = "{}",
        });
        var (service, _, scope) = await BeginAsync(root);
        await using var _ = scope;

        var preview = (await service.PreviewAsync("broken")).Value;
        var codes = Codes(preview).ToList();

        preview.CanImport.ShouldBeFalse();
        codes.ShouldContain("MANIFEST_LOOKUP");
        codes.ShouldContain("MANIFEST_PATH");
        codes.ShouldContain("MANIFEST_KIND");
        codes.ShouldContain("SOURCE_MISSING");       // barangays.csv cites nothing, nor do its rows
        codes.ShouldContain("INDEX_INVALID");        // province "97", barangay "001"
        codes.ShouldContain("DUPLICATE_KEY");        // PSGC 9700000001 and code DEMO-Z twice
        codes.ShouldContain("COLUMN_UNKNOWN");       // colour
        codes.ShouldContain("PARENT_NOT_FOUND");     // orphan municipality, material part
        codes.ShouldContain("BOOLEAN_INVALID");      // maybe
        codes.ShouldContain("PIN_INDEX_DUPLICATE");  // two municipalities numbered 05
        codes.ShouldContain("NUMBER_INVALID");       // sort_order "first"
        codes.ShouldContain("REQUIRED");             // DEMO-Y has no name
        codes.ShouldContain("NOT_YET_SUPPORTED");    // map layers (C5)
        codes.ShouldContain("FILE_UNREADABLE");      // missing.csv
        preview.Files.Single(f => f.Kind == "gis-layer").Supported.ShouldBeFalse();
        preview.Issues.ShouldContain(i => i.Code == "MANIFEST_PATH" && i.Field == "files[7]");

        (await service.PreviewAsync("../broken")).Code.ShouldBe("VALIDATION_FAILED");
        (await service.PreviewAsync("absent")).Code.ShouldBe("CONTENT_PACK_NOT_FOUND");
        Directory.Delete(root, recursive: true);
    }

    [Fact]
    public async Task ExistingRecords_ShowChangesAndMissingRows_AndLockedNumbersAreRefused()
    {
        var tag = Guid.NewGuid().ToString("N")[..6];
        var root = TempPack("changes", new Dictionary<string, string>
        {
            ["manifest.json"] = """
                { "schemaVersion": 1, "pack": "changes", "version": "T2", "files": [
                  { "kind": "provinces", "path": "p.csv", "source": "DEMO" },
                  { "kind": "barangays", "path": "b.csv", "source": "DEMO" } ] }
                """,
            ["p.csv"] = $"psgc_code,name,index_number\nCP-P{tag},DEMO Province renamed,996\n",
            ["b.csv"] = $"psgc_code,municipality_psgc,name,index_number\nCP-B1{tag},CP-M{tag},DEMO Barangay 1,0006\nCP-B3{tag},CP-M{tag},DEMO Barangay 3,0007\n",
        });
        var (service, db, scope) = await BeginAsync(root);
        await using var _ = scope;
        await db.Provinces.Where(x => x.PinIndexNumber == "997" || x.PinIndexNumber == "996").ExecuteUpdateAsync(s => s.SetProperty(x => x.PinIndexNumber, (string?)null));
        await db.Municipalities.Where(x => x.PinIndexNumber == "997" || x.PinIndexNumber == "996").ExecuteUpdateAsync(s => s.SetProperty(x => x.PinIndexNumber, (string?)null));

        var province = new Province { PsgcCode = $"CP-P{tag}", Name = "DEMO Province", PinIndexNumber = "997" };
        var town = new Municipality { Province = province, PsgcCode = $"CP-M{tag}", Name = "DEMO Town", PinIndexNumber = "03" };
        var b1 = new Barangay { Municipality = town, PsgcCode = $"CP-B1{tag}", Name = "DEMO Barangay 1", PinIndexNumber = "0005" };
        var b2 = new Barangay { Municipality = town, PsgcCode = $"CP-B2{tag}", Name = "DEMO Barangay 2", PinIndexNumber = "0007" };
        var section = new TaxMapSection { Barangay = b1, IndexNumber = "001" };
        db.AddRange(province, town, b1, b2, section);
        await db.SaveChangesAsync();
        var property = new PropertyEntity
        {
            PropertyIdentificationNumber = $"DEMO-CP-{tag}", ProvinceId = province.Id, MunicipalityId = town.Id, BarangayId = b1.Id,
        };
        var parcel = new Parcel { Property = property, BarangayId = b1.Id, SectionId = section.Id, ParcelNumber = 1 };
        db.AddRange(property, parcel);
        db.PinAssignments.Add(new PinAssignment
        {
            Property = property, Pin = $"DEMO-CP-{tag}", Kind = PinKind.Permanent, Parcel = parcel, BarangayId = b1.Id,
            SectionId = section.Id, ParcelNumber = 1, AssignedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();

        var preview = (await service.PreviewAsync("changes")).Value;
        var provinces = preview.Files.Single(f => f.Kind == "provinces");
        var barangays = preview.Files.Single(f => f.Kind == "barangays");

        provinces.Changed.ShouldBe(1);
        provinces.Changes.Single().Fields.Select(f => f.Field).ShouldBe(["name", "index_number"]);
        provinces.Issues.ShouldContain(i => i.Code == "PIN_INDEX_LOCKED" && i.Line == 2);
        provinces.MissingFromPack.ShouldBeGreaterThanOrEqualTo(0);

        barangays.Changed.ShouldBe(1);
        barangays.New.ShouldBe(1);
        barangays.Issues.ShouldContain(i => i.Code == "PIN_INDEX_LOCKED");      // barangay 1 carries a permanent PIN
        barangays.Issues.ShouldContain(i => i.Code == "PIN_INDEX_DUPLICATE");   // new barangay 3 takes barangay 2's 0007
        barangays.MissingFromPack.ShouldBe(1);
        barangays.MissingKeys.Single().ShouldStartWith($"CP-B2{tag}");
        barangays.Issues.ShouldContain(i => i.Code == "PSGC_FORMAT" && i.Severity == ContentIssueSeverity.Warning);
        preview.CanImport.ShouldBeFalse();

        (await db.Provinces.AsNoTracking().SingleAsync(x => x.Id == province.Id)).Name.ShouldBe("DEMO Province");
        Directory.Delete(root, recursive: true);
    }
}
