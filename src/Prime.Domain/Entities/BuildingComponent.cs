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

    /// <summary>
    /// An additional item (fence, gate, garage, balcony, mezzanine …; MRPAAO
    /// Att. 2) whose cost is added to the construction cost. Other components
    /// describe the structure and are not valued separately.
    /// </summary>
    public bool IsAdditionalItem { get; set; }

    /// <summary>The use portion the item belongs to; null: spread over the portions by floor area.</summary>
    public Guid? BuildingUsePortionId { get; set; }
}
