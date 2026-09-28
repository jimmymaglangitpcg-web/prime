using System.IO.Compression;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Prime.Application.Features.ContentPacks;
using Shouldly;
using Xunit;

namespace Prime.IntegrationTests;

/// <summary>
/// Step C4 of the LGU content pack (docs/analysis/lgu-content-pack.md): a pack
/// uploaded as a zip is unpacked safely into the content root, replacing the
/// pack of the same name while keeping the previous copy.
/// </summary>
public class ContentPackUploadTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private (IContentPackService Service, IAsyncDisposable Host, string Root) Begin(Dictionary<string, string?>? extra = null)
    {
        var root = Path.Combine(Path.GetTempPath(), $"prime-cp-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var settings = new Dictionary<string, string?> { ["ContentPacks:RootPath"] = root };
        foreach (var (k, v) in extra ?? [])
        {
            settings[k] = v;
        }
        var host = factory.WithWebHostBuilder(b => b.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(settings)));
        return (host.Services.CreateScope().ServiceProvider.GetRequiredService<IContentPackService>(), host, root);
    }

    private static MemoryStream Zip(params (string Name, string Content)[] entries)
    {
        var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, content) in entries)
            {
                using var writer = new StreamWriter(archive.CreateEntry(name).Open(), Encoding.UTF8);
                writer.Write(content);
            }
        }
        stream.Position = 0;
        return stream;
    }

    private const string Manifest = """{ "schemaVersion": 1, "pack": "uploaded", "version": "U1", "files": [ { "kind": "lookup", "lookup": "zones", "path": "lookups/zones.csv", "source": "DEMO" } ] }""";

    [Fact]
    public async Task AZippedFolder_IsUnpacked_AndReplacesThePackKeepingTheOldCopy()
    {
        var (service, host, root) = Begin();
        await using var _ = host;

        var first = await service.UploadAsync(Zip(("uploaded/manifest.json", Manifest), ("uploaded/lookups/zones.csv", "code,name\nDEMO-UZ,DEMO zone\n")));
        first.Value.Pack.ShouldBe("uploaded");
        File.ReadAllText(Path.Combine(root, "uploaded", "lookups", "zones.csv")).ShouldContain("DEMO-UZ");
        (await service.PreviewAsync("uploaded")).Value.CanImport.ShouldBeTrue();

        (await service.UploadAsync(Zip(("manifest.json", Manifest), ("lookups/zones.csv", "code,name\nDEMO-UZ2,DEMO zone two\n")))).IsSuccess.ShouldBeTrue();
        File.ReadAllText(Path.Combine(root, "uploaded", "lookups", "zones.csv")).ShouldContain("DEMO-UZ2");
        Directory.GetDirectories(Path.Combine(root, ".previous")).Single().ShouldContain("uploaded-");
        (await service.ListAsync()).Value.Select(p => p.Pack).ShouldBe(["uploaded"]); // .previous and staging folders are not packs
        Directory.Delete(root, recursive: true);
    }

    [Fact]
    public async Task UnsafeOrOversizedZips_AreRefused_AndLeaveNothingBehind()
    {
        var (service, host, root) = Begin(new() { ["ContentPacks:MaxUnpackedBytes"] = "2000" });
        await using var _ = host;

        (await service.UploadAsync(new MemoryStream("not a zip"u8.ToArray()))).Code.ShouldBe("CONTENT_PACK_UPLOAD_INVALID");
        (await service.UploadAsync(Zip(("readme.txt", "no manifest")))).Message.ShouldNotBeNull().ShouldContain("manifest.json");
        (await service.UploadAsync(Zip(("manifest.json", Manifest), ("../escape.csv", "x")))).Message.ShouldNotBeNull().ShouldContain("escape.csv");
        (await service.UploadAsync(Zip(("manifest.json", Manifest.Replace("\"uploaded\"", "\"../evil\""))))).Message.ShouldNotBeNull().ShouldContain("pack");
        (await service.UploadAsync(Zip(("manifest.json", Manifest), ("lookups/zones.csv", new string('x', 5000))))).Message.ShouldNotBeNull().ShouldContain("allowed size");

        Directory.GetFileSystemEntries(root).ShouldBeEmpty(); // no pack, no staging folder left over
        Directory.Delete(root, recursive: true);
    }
}
