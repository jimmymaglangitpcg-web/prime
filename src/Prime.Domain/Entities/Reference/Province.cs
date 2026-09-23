using Prime.Domain.Common;

namespace Prime.Domain.Entities.Reference;

/// <summary>
/// CLAUDE.md §27 "Province". <see cref="PsgcCode"/> is the official
/// Philippine Standard Geographic Code from the Philippine Statistics
/// Authority — a public structural identifier (not a legal/tax value), safe
/// to include without domain-verification per CLAUDE.md §5/§6.
/// </summary>
public sealed class Province : AuditableEntity
{
    public string PsgcCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}
