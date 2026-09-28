using Prime.Domain.Common;
using Prime.Domain.Enums;

namespace Prime.Domain.Entities.Content;

/// <summary>
/// One applied LGU content pack (docs/analysis/lgu-content-pack.md §3.4):
/// which pack and version, the exact files (SHA-256 of the manifest and of
/// each file, in <see cref="FilesJson"/>), who confirmed it and what it did.
/// <see cref="Fingerprint"/> is the hash the user previewed; an import
/// applies only if the files still hash to it. Never edited or deleted
/// (CLAUDE.md §49); an import that would change nothing is not recorded.
/// </summary>
public sealed class ContentImport : AuditableEntity
{
    public string Pack { get; set; } = string.Empty;
    public string PackVersion { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string ManifestSha256 { get; set; } = string.Empty;
    public string Fingerprint { get; set; } = string.Empty;
    public DateTimeOffset ImportedAt { get; set; }
    public Guid ImportedBy { get; set; }
    public int CreatedCount { get; set; }
    public int ChangedCount { get; set; }
    /// <summary>Per file: kind, lookup, path, SHA-256, source and counts.</summary>
    public string FilesJson { get; set; } = "[]";
    /// <summary>The preview's warnings at the time of import (errors block an import).</summary>
    public string WarningsJson { get; set; } = "[]";
    public List<ContentImportItem> Items { get; set; } = [];
}

/// <summary>
/// A record an import created or changed, with the citation it came from and
/// the file and line that carried it — the provenance of LGU and LAM content
/// (CLAUDE.md §6, §77), kept here rather than on each configured table.
/// </summary>
public sealed class ContentImportItem : Entity
{
    public Guid ContentImportId { get; set; }
    public int Sequence { get; set; }
    /// <summary>The table's entity name, e.g. "Barangay", "Classification".</summary>
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    /// <summary>The natural key: PSGC code or lookup code.</summary>
    public string Key { get; set; } = string.Empty;
    public ContentImportAction Action { get; set; }
    /// <summary>Field changes as JSON ([{field, from, to}]).</summary>
    public string ChangesJson { get; set; } = "[]";
    public string Source { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public int Line { get; set; }
}
