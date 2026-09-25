using Prime.Domain.Common;
using Prime.Domain.Entities.Reference;
using Prime.Domain.Enums;

namespace Prime.Domain.Entities;

/// <summary>
/// Links a party to a Property in a statutory capacity (<see cref="Role"/>,
/// LGC §§204–205) with a share, effective-dated (CLAUDE.md §20). History is
/// append-only: a change inserts a new row and ends the previous one — never
/// overwritten. An <see cref="PropertyPartyRole.UnknownOwner"/> row has no
/// taxpayer; every other role has one. Only Owner rows carry an ownership
/// type and count toward the 100% ownership total.
/// </summary>
public sealed class PropertyTaxpayer : AuditableEntity
{
    public Guid PropertyId { get; set; }
    public PropertyEntity? Property { get; set; }
    public PropertyPartyRole Role { get; set; } = PropertyPartyRole.Owner;

    /// <summary>
    /// Null: a party of the whole property. Set: a party of that unit only —
    /// e.g. a building owned by someone other than the landowner (MRPAAO p.42).
    /// A unit with current parties of its own uses them; otherwise the property's.
    /// </summary>
    public Guid? RpuId { get; set; }
    public RealPropertyUnit? Rpu { get; set; }

    public Guid? TaxpayerId { get; set; }
    public Taxpayer? Taxpayer { get; set; }

    public Guid? OwnershipTypeId { get; set; }
    public OwnershipType? OwnershipType { get; set; }
    public decimal OwnershipPercentage { get; set; }

    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public bool IsCurrent { get; set; } = true;
    /// <summary>Why the row was ended, e.g. "owner identified" for an unknown-owner declaration.</summary>
    public string? EndReason { get; set; }

    /// <summary>The transaction (e.g. a transfer) that started or ended this link, if any (CLAUDE.md §35).</summary>
    public Guid? StartedByTransactionId { get; set; }
    public Guid? EndedByTransactionId { get; set; }
}
