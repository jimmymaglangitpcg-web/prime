using System.Text.Json.Serialization;

namespace Prime.Application.Features.ContentPacks;

// --- Manifest (docs/analysis/lgu-content-pack.md §3.1) ---

/// <summary>
/// <c>manifest.json</c> at the root of a pack folder. <see cref="Pack"/> must
/// equal the folder name; <see cref="Files"/> lists every file PRIME reads, in
/// load order, each with the citation of the document it comes from.
/// </summary>
public sealed record ContentPackManifest(
    [property: JsonPropertyName("schemaVersion")] int SchemaVersion,
    [property: JsonPropertyName("pack")] string? Pack,
    [property: JsonPropertyName("version")] string? Version,
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("files")] IReadOnlyList<ContentPackManifestFile>? Files);

public sealed record ContentPackManifestFile(
    [property: JsonPropertyName("kind")] string? Kind,
    [property: JsonPropertyName("path")] string? Path,
    [property: JsonPropertyName("lookup")] string? Lookup,
    [property: JsonPropertyName("source")] string? Source);

/// <summary>File kinds a manifest may name, and the step from which PRIME imports them (§5).</summary>
public static class ContentFileKinds
{
    public const string Provinces = "provinces";
    public const string Municipalities = "municipalities";
    public const string Barangays = "barangays";
    public const string Lookup = "lookup";

    /// <summary>Validated and previewed from step C1.</summary>
    public static readonly IReadOnlySet<string> Supported = new HashSet<string> { Provinces, Municipalities, Barangays, Lookup };

    /// <summary>Recognised, hashed and listed, but not yet read: the step that adds each.</summary>
    public static readonly IReadOnlyDictionary<string, string> Later = new Dictionary<string, string>
    {
        ["transaction-types"] = "C3",
        ["numbering-schemes"] = "C3",
        ["approval-chains"] = "C3",
        ["forms"] = "C3",
        ["form-template"] = "C3",
        ["gis-layer"] = "C5",
        ["smv"] = "L1-3",
        ["smv-schedules"] = "L1-3",
        ["adjustment-factors"] = "L1-3",
        ["assessment-levels"] = "L1-3",
    };
}

// --- Reading packs (implemented in Infrastructure) ---

/// <summary>A pack folder found under the configured content root.</summary>
public sealed record ContentPackInfo(string Pack, bool HasManifest);

/// <summary>A file's bytes, or why it could not be read (missing, too large, outside the pack).</summary>
public sealed record ContentFileRead(byte[]? Content, string? Error);

// --- Preview ---

public enum ContentIssueSeverity
{
    Error = 0,
    Warning = 1,
}

public enum ContentChangeAction
{
    New = 0,
    Changed = 1,
}

/// <summary>One problem found; <see cref="Line"/> is the 1-based line in the file (the header is line 1).</summary>
public sealed record ContentIssueDto(ContentIssueSeverity Severity, string Code, string Message, int? Line, string? Field);

public sealed record ContentFieldChangeDto(string Field, string? From, string? To);

/// <summary>A row that would be added or changed, keyed by its natural key (PSGC code or lookup code).</summary>
public sealed record ContentChangeDto(string Key, string Name, ContentChangeAction Action, IReadOnlyList<ContentFieldChangeDto> Fields);

/// <summary>
/// The dry-run result for one manifest file. <see cref="MissingFromPack"/>
/// counts existing records in the file's scope that the file does not list:
/// they are reported, never removed (decision Q4). <see cref="Changes"/> is
/// capped at <see cref="ContentPackService.ChangeListLimit"/> entries.
/// </summary>
public sealed record ContentFilePreviewDto(
    string Kind,
    string? Lookup,
    string Path,
    string? Source,
    string? Sha256,
    bool Supported,
    int Rows,
    int New,
    int Changed,
    int Unchanged,
    int MissingFromPack,
    IReadOnlyList<string> MissingKeys,
    IReadOnlyList<ContentChangeDto> Changes,
    IReadOnlyList<ContentIssueDto> Issues);

/// <summary>
/// A dry run of a whole pack. Nothing is written. <see cref="CanImport"/> is false while any error remains.
/// <see cref="Fingerprint"/> hashes the manifest and every file; an import must present it, so what is applied is what was previewed.
/// </summary>
public sealed record ContentPackPreviewDto(
    string Pack,
    string? Version,
    string? Description,
    string? ManifestSha256,
    string? Fingerprint,
    bool CanImport,
    int ErrorCount,
    int WarningCount,
    IReadOnlyList<ContentIssueDto> Issues,
    IReadOnlyList<ContentFilePreviewDto> Files);

// --- Import (step C2) ---

public sealed record ImportContentPackRequest(string? Fingerprint);

public sealed record ContentImportFileDto(string Kind, string? Lookup, string Path, string? Sha256, string? Source, int Created, int Changed, int Unchanged);

public sealed record ContentImportDto(
    Guid Id,
    string Pack,
    string PackVersion,
    string? Description,
    string ManifestSha256,
    string Fingerprint,
    DateTimeOffset ImportedAt,
    Guid ImportedBy,
    string? ImportedByName,
    int CreatedCount,
    int ChangedCount,
    IReadOnlyList<ContentImportFileDto> Files,
    IReadOnlyList<ContentIssueDto> Warnings);

public sealed record ContentImportItemDto(
    int Sequence,
    string EntityType,
    Guid EntityId,
    string Key,
    Prime.Domain.Enums.ContentImportAction Action,
    IReadOnlyList<ContentFieldChangeDto> Changes,
    string Source,
    string FilePath,
    int Line);

/// <summary><see cref="Applied"/> is false when the pack matched PRIME already; nothing was written or recorded.</summary>
public sealed record ContentImportResultDto(bool Applied, string Message, ContentImportDto? Import);
