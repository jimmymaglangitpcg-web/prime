using System.IO.Compression;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Prime.Application.Common;
using Prime.Application.Common.Interfaces;
using Prime.Application.Features.ContentPacks;

namespace Prime.Infrastructure.ContentPacks;

/// <summary>
/// "ContentPacks" configuration section (docs/analysis/lgu-content-pack.md §3.5).
/// <see cref="RootPath"/> is the folder holding the packs, e.g. the repository's
/// gitignored <c>lgu-content/</c>; set it with the environment variable
/// <c>ContentPacks__RootPath</c>. Unset: content packs are unavailable.
/// </summary>
public sealed class ContentPackOptions
{
    public const string SectionName = "ContentPacks";

    public string? RootPath { get; set; }

    /// <summary>Largest file PRIME reads from a pack, in bytes.</summary>
    public long MaxFileBytes { get; set; } = 50 * 1024 * 1024;

    /// <summary>Largest uploaded zip, and the most its files may unpack to, in bytes.</summary>
    public long MaxUploadBytes { get; set; } = 100 * 1024 * 1024;

    public long MaxUnpackedBytes { get; set; } = 500 * 1024 * 1024;

    public int MaxUploadEntries { get; set; } = 5000;
}

/// <summary>
/// Reads packs from the server's content root. Pack names and file paths come
/// from users and manifests, so each is resolved and then required to stay
/// inside the root (and the pack folder); symbolic links that point outside
/// are refused the same way. The client never supplies a filesystem path.
/// </summary>
public sealed class FileSystemContentPackSource(IOptions<ContentPackOptions> options) : IContentPackSource
{
    public Task<Result<IReadOnlyList<ContentPackInfo>>> ListAsync(CancellationToken cancellationToken = default)
    {
        if (Root() is not { } root)
        {
            return Task.FromResult(NotConfigured<IReadOnlyList<ContentPackInfo>>());
        }
        IReadOnlyList<ContentPackInfo> packs = Directory.EnumerateDirectories(root)
            .Select(d => new DirectoryInfo(d))
            .Where(d => (d.Attributes & FileAttributes.ReparsePoint) == 0 && ContentPackService.IsPackName(d.Name))
            .Select(d => new ContentPackInfo(d.Name, File.Exists(Path.Combine(d.FullName, ContentPackService.ManifestFile))))
            .OrderBy(p => p.Pack, StringComparer.Ordinal)
            .ToList();
        return Task.FromResult(Result.Success(packs));
    }

    public async Task<Result<ContentFileRead>> ReadAsync(string pack, string relativePath, CancellationToken cancellationToken = default)
    {
        if (Root() is not { } root)
        {
            return NotConfigured<ContentFileRead>();
        }
        var packDir = Path.GetFullPath(Path.Combine(root, pack));
        if (!Inside(root, packDir) || !Directory.Exists(packDir) || (new DirectoryInfo(packDir).Attributes & FileAttributes.ReparsePoint) != 0)
        {
            return Result.Failure<ContentFileRead>("CONTENT_PACK_NOT_FOUND", $"No content pack named '{pack}' was found.");
        }
        if (ContentPackService.PathProblem(relativePath) is { } problem)
        {
            return Result.Success(new ContentFileRead(null, $"'{relativePath}': {problem}"));
        }

        var full = Path.GetFullPath(Path.Combine(packDir, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!Inside(packDir, full))
        {
            return Result.Success(new ContentFileRead(null, $"'{relativePath}' is outside the pack folder."));
        }
        var file = new FileInfo(full);
        if (!file.Exists)
        {
            return Result.Success(new ContentFileRead(null, $"'{relativePath}' does not exist in the pack."));
        }
        if (file.LinkTarget is not null && !Inside(packDir, Path.GetFullPath(file.ResolveLinkTarget(true)!.FullName)))
        {
            return Result.Success(new ContentFileRead(null, $"'{relativePath}' links outside the pack folder."));
        }
        if (file.Length > options.Value.MaxFileBytes)
        {
            return Result.Success(new ContentFileRead(null, $"'{relativePath}' is larger than {options.Value.MaxFileBytes:N0} bytes."));
        }
        return Result.Success(new ContentFileRead(await File.ReadAllBytesAsync(full, cancellationToken), null));
    }

    public async Task<Result<ContentPackInfo>> SaveUploadAsync(Stream zip, CancellationToken cancellationToken = default)
    {
        if (Root() is not { } root)
        {
            return NotConfigured<ContentPackInfo>();
        }
        var limits = options.Value;

        // Buffer with a hard cap, so an oversized upload is refused without reading it all.
        await using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int n;
        while ((n = await zip.ReadAsync(chunk, cancellationToken)) > 0)
        {
            if (buffer.Length + n > limits.MaxUploadBytes)
            {
                return Invalid($"The zip is larger than {limits.MaxUploadBytes:N0} bytes.");
            }
            buffer.Write(chunk, 0, n);
        }
        buffer.Position = 0;

        ZipArchive archive;
        try
        {
            archive = new ZipArchive(buffer, ZipArchiveMode.Read, leaveOpen: true);
        }
        catch (InvalidDataException)
        {
            return Invalid("The file is not a readable zip archive.");
        }
        using (archive)
        {
            var files = archive.Entries.Where(e => !e.FullName.EndsWith('/')).ToList();
            if (files.Count > limits.MaxUploadEntries)
            {
                return Invalid($"The zip holds {files.Count} files; at most {limits.MaxUploadEntries} are allowed.");
            }

            // The manifest may sit at the root, or inside one top-level folder (as when a folder is zipped).
            var prefix = "";
            if (files.All(e => e.FullName != ContentPackService.ManifestFile))
            {
                var wrapped = files.Where(e => e.FullName.Count(c => c == '/') == 1 && e.FullName.EndsWith("/" + ContentPackService.ManifestFile)).ToList();
                if (wrapped.Count != 1 || files.Any(e => !e.FullName.StartsWith(wrapped[0].FullName[..^ContentPackService.ManifestFile.Length], StringComparison.Ordinal)))
                {
                    return Invalid($"The zip has no {ContentPackService.ManifestFile} at its root or inside a single top-level folder.");
                }
                prefix = wrapped[0].FullName[..^ContentPackService.ManifestFile.Length];
            }

            string? pack;
            try
            {
                await using var manifestStream = await files.Single(e => e.FullName == prefix + ContentPackService.ManifestFile).OpenAsync(cancellationToken);
                using var manifest = await JsonDocument.ParseAsync(manifestStream, cancellationToken: cancellationToken);
                pack = manifest.RootElement.TryGetProperty("pack", out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;
            }
            catch (JsonException)
            {
                return Invalid($"{ContentPackService.ManifestFile} is not valid JSON.");
            }
            if (!ContentPackService.IsPackName(pack))
            {
                return Invalid("The manifest's \"pack\" must be 1–64 characters: lower-case letters, digits, '-' or '_'.");
            }

            var staging = Path.Combine(root, $".upload-{Guid.NewGuid():N}");
            Directory.CreateDirectory(staging);
            try
            {
                long unpacked = 0;
                foreach (var entry in files)
                {
                    var relative = entry.FullName[prefix.Length..];
                    if (ContentPackService.PathProblem(relative) is { } problem)
                    {
                        return Invalid($"Entry '{entry.FullName}': {problem}");
                    }
                    if (entry.Length > limits.MaxFileBytes)
                    {
                        return Invalid($"Entry '{entry.FullName}' unpacks to more than {limits.MaxFileBytes:N0} bytes.");
                    }
                    var target = Path.GetFullPath(Path.Combine(staging, relative.Replace('/', Path.DirectorySeparatorChar)));
                    if (!Inside(staging, target))
                    {
                        return Invalid($"Entry '{entry.FullName}' would land outside the pack.");
                    }
                    Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                    // Count the bytes actually written: an entry's declared length can lie.
                    await using var input = await entry.OpenAsync(cancellationToken);
                    await using var output = File.Create(target);
                    int read;
                    while ((read = await input.ReadAsync(chunk, cancellationToken)) > 0)
                    {
                        unpacked += read;
                        if (unpacked > limits.MaxUnpackedBytes || output.Length + read > limits.MaxFileBytes)
                        {
                            return Invalid($"The zip unpacks to more than the allowed size (per file {limits.MaxFileBytes:N0} bytes, in total {limits.MaxUnpackedBytes:N0}).");
                        }
                        await output.WriteAsync(chunk.AsMemory(0, read), cancellationToken);
                    }
                }

                // Replace the pack: the previous copy is kept under .previous/, never deleted.
                var packDir = Path.Combine(root, pack!);
                if (Directory.Exists(packDir))
                {
                    var previous = Path.Combine(root, ".previous");
                    Directory.CreateDirectory(previous);
                    Directory.Move(packDir, Path.Combine(previous, $"{pack}-{DateTimeOffset.UtcNow:yyyyMMddTHHmmssfffZ}"));
                }
                Directory.Move(staging, packDir);
                return Result.Success(new ContentPackInfo(pack!, true));
            }
            finally
            {
                if (Directory.Exists(staging))
                {
                    Directory.Delete(staging, recursive: true); // only the unfinished upload's own copy
                }
            }
        }
    }

    private static Result<ContentPackInfo> Invalid(string message) => Result.Failure<ContentPackInfo>("CONTENT_PACK_UPLOAD_INVALID", message);

    private string? Root()
    {
        var configured = options.Value.RootPath;
        if (string.IsNullOrWhiteSpace(configured))
        {
            return null;
        }
        var root = Path.GetFullPath(configured);
        return Directory.Exists(root) ? root : null;
    }

    private static bool Inside(string parent, string child)
    {
        var p = Path.TrimEndingDirectorySeparator(parent) + Path.DirectorySeparatorChar;
        return child.StartsWith(p, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    }

    private static Result<T> NotConfigured<T>() =>
        Result.Failure<T>("CONTENT_PACKS_NOT_CONFIGURED",
            "No content pack folder is configured, or it does not exist. Set ContentPacks:RootPath (environment variable ContentPacks__RootPath).");
}
