using Prime.Application.Features.ContentPacks;

namespace Prime.Application.Common.Interfaces;

/// <summary>
/// Reads LGU content packs (docs/analysis/lgu-content-pack.md §3.5) from the
/// content root configured for the deployment. The pack name and every file
/// path are confined to that root: a name or path that would leave it is
/// refused, never resolved.
/// </summary>
public interface IContentPackSource
{
    /// <summary>The pack folders under the root; failure when no root is configured or it does not exist.</summary>
    Task<Result<IReadOnlyList<ContentPackInfo>>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>A file of <paramref name="pack"/>, by its path relative to the pack folder (forward slashes).</summary>
    Task<Result<ContentFileRead>> ReadAsync(string pack, string relativePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Unpacks an uploaded zip into the content root as the pack its manifest names,
    /// replacing that pack's folder (the previous copy is moved aside, never deleted).
    /// Refuses unsafe entries, oversized content and names that fail the pack rules.
    /// </summary>
    Task<Result<ContentPackInfo>> SaveUploadAsync(Stream zip, CancellationToken cancellationToken = default);
}
