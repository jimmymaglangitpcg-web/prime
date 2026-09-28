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
            .Where(d => (d.Attributes & FileAttributes.ReparsePoint) == 0)
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
