using Prime.Domain.Common;
using Prime.Domain.Entities.Reference;

namespace Prime.Domain.Entities;

/// <summary>
/// Links a Taxpayer to a Property with an ownership share, effective-dated
/// (CLAUDE.md §20). Ownership history is append-only: a change of owner
/// inserts a new row and closes the previous one — never overwritten.
/// </summary>
public sealed class PropertyTaxpayer : AuditableEntity
{
    public Guid PropertyId { get; set; }
    public PropertyEntity? Property { get; set; }
    public Guid TaxpayerId { get; set; }
    public Taxpayer? Taxpayer { get; set; }

    public Guid OwnershipTypeId { get; set; }
    public OwnershipType? OwnershipType { get; set; }
    public decimal OwnershipPercentage { get; set; }

    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public bool IsCurrent { get; set; } = true;
}
