using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Prime.Application.Common;
using Prime.Application.Features.ContentPacks;
using Prime.Domain.Entities.Audit;
using Prime.Domain.Entities.Identity;
using Prime.Domain.Entities.Reference;
using Prime.Domain.Enums;
using Prime.Infrastructure.Identity;
using Prime.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Prime.IntegrationTests;

/// <summary>
/// Step C2 of the LGU content pack (docs/analysis/lgu-content-pack.md): import
/// applies exactly the previewed plan, records its provenance, is idempotent and
/// refuses changed or invalid packs. Everything is rolled back.
/// </summary>
public class ContentPackImportTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private sealed record Ctx(IContentPackService Service, PrimeDbContext Db, CurrentUserService User, AppUser Importer);

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
        var user = scope.ServiceProvider.GetRequiredService<CurrentUserService>();
        user.AppUserId = importer.Id;
        return (new Ctx(scope.ServiceProvider.GetRequiredService<IContentPackService>(), db, user, importer), new Disposer(transaction, scope, host));
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

    [Fact]
    public async Task DemoPack_ImportsWithProvenance_AndASecondImportChangesNothing()
    {
        var (c, scope) = await BeginAsync(Path.Combine(RepoRoot(), "samples"));
        await using var _ = scope;
        var imports = await c.Db.ContentImports.CountAsync();

        var preview = (await c.Service.PreviewAsync("content-demo")).Value;
        preview.CanImport.ShouldBeTrue();
        var result = (await c.Service.ImportAsync("content-demo", new(preview.Fingerprint))).Value;

        result.Applied.ShouldBeTrue();
        var record = result.Import.ShouldNotBeNull();
        record.CreatedCount.ShouldBe(17); // 1 province, 2 towns, 4 barangays, 2 classifications, 2 parts, 2 materials, 4 draft versions
        record.ChangedCount.ShouldBe(0);
        record.ImportedBy.ShouldBe(c.Importer.Id);
        record.ImportedByName.ShouldBe("DEMO Importer");
        record.Fingerprint.ShouldBe(preview.Fingerprint);
        record.Files.Count.ShouldBe(10);

        var province = await c.Db.Provinces.SingleAsync(x => x.PsgcCode == "9900000000");
        province.PinIndexNumber.ShouldBe("998");
        var town = await c.Db.Municipalities.SingleAsync(x => x.PsgcCode == "9900200000");
        (town.ProvinceId, town.IsCity, town.PinIndexNumber).ShouldBe((province.Id, false, "02"));
        (await c.Db.Barangays.SingleAsync(x => x.PsgcCode == "9900100001")).Name.ShouldBe("DEMO Poblacion, Town A");
        (await c.Db.Barangays.SingleAsync(x => x.PsgcCode == "9900200002")).PinIndexNumber.ShouldBeNull();
        var material = await c.Db.StructuralMaterials.Include(x => x.StructuralPart).SingleAsync(x => x.Code == "DEMO-CP-F-RC");
        material.StructuralPart!.Code.ShouldBe("DEMO-CP-FOUNDATION");
        var classification = await c.Db.Classifications.SingleAsync(x => x.Code == "DEMO-CP-R");
        (classification.SortOrder, classification.IsActive, classification.Description).ShouldBe((10, true, "DEMO classification"));

        var items = (await c.Service.ListImportItemsAsync(record.Id, new PagedRequest { PageSize = 100 })).Value;
        items.TotalCount.ShouldBe(17);
        items.Items.ShouldAllBe(i => i.Action == ContentImportAction.Created && i.Source.Length > 0 && i.Line >= 1);
        items.Items.Single(i => i.Key == "9900200001").Source.ShouldBe("DEMO data (row-level source)");
        items.Items.Single(i => i.Key == "9900200001").EntityId.ShouldBe((await c.Db.Barangays.SingleAsync(x => x.PsgcCode == "9900200001")).Id);
        (await c.Db.Set<AuditLog>().AnyAsync(a => a.RecordId == province.Id && a.Reason == "Content pack content-demo DEMO-1")).ShouldBeTrue();

        var again = (await c.Service.ImportAsync("content-demo", new(preview.Fingerprint))).Value;
        again.Applied.ShouldBeFalse();
        again.Import.ShouldBeNull();
        (await c.Db.ContentImports.CountAsync()).ShouldBe(imports + 1);
        (await c.Service.ListImportsAsync("content-demo", new PagedRequest())).Value.Items.ShouldContain(x => x.Id == record.Id);
    }

    [Fact]
    public async Task Import_RefusesAStaleFingerprint_AnInvalidPack_AndAnUnknownUser()
    {
        var root = TempPack("guard", new Dictionary<string, string>
        {
            ["manifest.json"] = """{ "schemaVersion": 1, "pack": "guard", "version": "T1", "files": [ { "kind": "lookup", "lookup": "zones", "path": "z.csv", "source": "DEMO" } ] }""",
            ["z.csv"] = "code,name\nDEMO-GZ1,DEMO Zone 1\n",
        });
        var (c, scope) = await BeginAsync(root);
        await using var _ = scope;
        var preview = (await c.Service.PreviewAsync("guard")).Value;

        File.WriteAllText(Path.Combine(root, "guard", "z.csv"), "code,name\nDEMO-GZ1,DEMO Zone 1 edited\n");
        (await c.Service.ImportAsync("guard", new(preview.Fingerprint))).Code.ShouldBe("CONTENT_PACK_CONFLICT");
        (await c.Service.ImportAsync("guard", new(null))).Code.ShouldBe("VALIDATION_FAILED");

        File.WriteAllText(Path.Combine(root, "guard", "z.csv"), "code,name\nDEMO-GZ1,\n");
        var broken = (await c.Service.PreviewAsync("guard")).Value;
        broken.CanImport.ShouldBeFalse();
        (await c.Service.ImportAsync("guard", new(broken.Fingerprint))).Code.ShouldBe("CONTENT_PACK_INVALID");

        c.User.AppUserId = null;
        (await c.Service.ImportAsync("guard", new(broken.Fingerprint))).Code.ShouldBe("IMPORTER_UNKNOWN");
        (await c.Db.Zones.AnyAsync(x => x.Code == "DEMO-GZ1")).ShouldBeFalse();
        Directory.Delete(root, recursive: true);
    }

    [Fact]
    public async Task ExistingRecords_AreUpdated_IndexNumbersCanSwap_AndBlankCellsKeepValues()
    {
        var tag = Guid.NewGuid().ToString("N")[..6];
        var root = TempPack("update", new Dictionary<string, string>
        {
            ["manifest.json"] = """
                { "schemaVersion": 1, "pack": "update", "version": "T3", "files": [
                  { "kind": "barangays", "path": "b.csv", "source": "DEMO" },
                  { "kind": "lookup", "lookup": "road-types", "path": "r.csv", "source": "DEMO" } ] }
                """,
            ["b.csv"] = $"psgc_code,municipality_psgc,name,index_number\nCU-B1{tag},CU-M{tag},DEMO One renamed,0002\nCU-B2{tag},CU-M{tag},DEMO Two,0001\n",
            ["r.csv"] = $"code,name,description,sort_order,is_active\nDEMO-RT{tag},DEMO Road renamed,,,no\n",
        });
        var (c, scope) = await BeginAsync(root);
        await using var _ = scope;
        var province = new Province { PsgcCode = $"CU-P{tag}", Name = "DEMO Province" };
        var town = new Municipality { Province = province, PsgcCode = $"CU-M{tag}", Name = "DEMO Town" };
        var one = new Barangay { Municipality = town, PsgcCode = $"CU-B1{tag}", Name = "DEMO One", PinIndexNumber = "0001" };
        var two = new Barangay { Municipality = town, PsgcCode = $"CU-B2{tag}", Name = "DEMO Two", PinIndexNumber = "0002" };
        var road = new RoadType { Code = $"DEMO-RT{tag}", Name = "DEMO Road", Description = "kept", SortOrder = 7 };
        c.Db.AddRange(province, town, one, two, road);
        await c.Db.SaveChangesAsync();

        var preview = (await c.Service.PreviewAsync("update")).Value;
        preview.CanImport.ShouldBeTrue();
        var record = (await c.Service.ImportAsync("update", new(preview.Fingerprint))).Value.Import.ShouldNotBeNull();

        record.ChangedCount.ShouldBe(3);
        c.Db.ChangeTracker.Clear();
        (await c.Db.Barangays.SingleAsync(x => x.Id == one.Id)).ShouldSatisfyAllConditions(
            b => b.Name.ShouldBe("DEMO One renamed"), b => b.PinIndexNumber.ShouldBe("0002"));
        (await c.Db.Barangays.SingleAsync(x => x.Id == two.Id)).PinIndexNumber.ShouldBe("0001");
        var updated = await c.Db.RoadTypes.SingleAsync(x => x.Id == road.Id);
        (updated.Name, updated.Description, updated.SortOrder, updated.IsActive).ShouldBe(("DEMO Road renamed", "kept", 7, false));

        var items = (await c.Service.ListImportItemsAsync(record.Id, new PagedRequest())).Value.Items;
        items.Single(i => i.Key == $"CU-B1{tag}").Changes.Select(x => (x.Field, x.From, x.To))
            .ShouldBe([("name", "DEMO One", "DEMO One renamed"), ("index_number", "0001", "0002")]);
        items.Single(i => i.EntityType == nameof(RoadType)).Changes.Select(x => x.Field).ShouldBe(["name", "is_active"]);
        Directory.Delete(root, recursive: true);
    }
}
