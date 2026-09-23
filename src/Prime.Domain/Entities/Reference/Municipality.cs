using Prime.Domain.Common;

namespace Prime.Domain.Entities.Reference;

/// <summary>CLAUDE.md §27 "City/Municipality".</summary>
public sealed class Municipality : AuditableEntity
{
    public Guid ProvinceId { get; set; }
    public Province? Province { get; set; }
    public string PsgcCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsCity { get; set; }
    public bool IsActive { get; set; } = true;
}
