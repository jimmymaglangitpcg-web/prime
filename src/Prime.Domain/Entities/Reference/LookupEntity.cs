using Prime.Domain.Common;

namespace Prime.Domain.Entities.Reference;

/// <summary>
/// Base shape for the LGU-configurable classification/lookup tables called
/// out in CLAUDE.md §27 (Classification, Actual Use, Sub-Classification,
/// Building Type, Structural Type, Building Component Type, Machinery
/// Type, Road Type, Condition, Ownership Type, Document Type, ...). Each
/// concrete subtype is its own table (not a shared discriminated table) so
/// foreign keys stay type-safe and each catalog can evolve independently —
/// see docs/DOMAIN-MODEL.md §3.10.
/// </summary>
public abstract class LookupEntity : AuditableEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}
