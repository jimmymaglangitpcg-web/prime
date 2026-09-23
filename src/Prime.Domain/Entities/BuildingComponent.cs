using Prime.Domain.Common;
using Prime.Domain.Entities.Reference;

namespace Prime.Domain.Entities;

/// <summary>CLAUDE.md §25 — component types and costs must be configurable.</summary>
public sealed class BuildingComponent : AuditableEntity
{
    public Guid BuildingId { get; set; }
    public Building? Building { get; set; }

    public Guid ComponentTypeId { get; set; }
    public BuildingComponentType? ComponentType { get; set; }

    public string? Description { get; set; }
    public decimal? Quantity { get; set; }
    public decimal? UnitCost { get; set; }
    public decimal? Cost { get; set; }
}
