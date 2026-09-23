using Prime.Domain.Common;
using Prime.Domain.Entities.Reference;
using Prime.Domain.Enums;

namespace Prime.Domain.Entities.Documents;

/// <summary>
/// Metadata for a file held in Supabase Storage (CLAUDE.md §59,
/// docs/ARCHITECTURE.md §3.9, docs/DOMAIN-MODEL.md §3.19a). Never stores
/// file bytes — only the storage reference. Access is always mediated by
/// Prime.WebApi (authorization check, then stream or short-lived signed
/// URL); a row existing here does not imply a public URL.
/// </summary>
public sealed class Document : AuditableEntity
{
    public string Bucket { get; set; } = string.Empty;
    public string StoragePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }

    public Guid DocumentTypeId { get; set; }
    public DocumentType? DocumentType { get; set; }

    /// <summary>e.g. "Property", "PropertyExemption" — the record this document supports.</summary>
    public string RelatedEntityType { get; set; } = string.Empty;
    public Guid RelatedEntityId { get; set; }

    public Guid? UploadedBy { get; set; }
    public RecordStatus Status { get; set; } = RecordStatus.Active;
}
