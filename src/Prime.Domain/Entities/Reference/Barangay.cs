using Prime.Domain.Common;

namespace Prime.Domain.Entities.Reference;

/// <summary>CLAUDE.md §27 "Barangay".</summary>
public sealed class Barangay : AuditableEntity
{
    public Guid MunicipalityId { get; set; }
    public Municipality? Municipality { get; set; }
    public string PsgcCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}
