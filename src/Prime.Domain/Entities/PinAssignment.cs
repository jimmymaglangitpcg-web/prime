using Prime.Domain.Common;
using Prime.Domain.Entities.Reference;
using Prime.Domain.Enums;

namespace Prime.Domain.Entities;

/// <summary>
/// One PIN a property has carried (docs/analysis/property-identification.md §3.4;
/// MRPAAO Ch. II §1 D.4): the PIN given at registration (typed, or temporary before
/// tax mapping), then the permanent PIN from its tax map section and parcel number.
/// A property has one current assignment (<see cref="RetiredAt"/> null); a PIN is
/// never given twice, so retired PINs stay reserved. Rows are never deleted.
/// </summary>
public sealed class PinAssignment : AuditableEntity
{
    public Guid PropertyId { get; set; }
    public PropertyEntity? Property { get; set; }
    public string Pin { get; set; } = string.Empty;
    public PinKind Kind { get; set; }

    /// <summary>For a permanent PIN: where it comes from.</summary>
    public Guid? ParcelId { get; set; }
    public Parcel? Parcel { get; set; }
    public Guid? BarangayId { get; set; }
    public Barangay? Barangay { get; set; }
    public Guid? SectionId { get; set; }
    public TaxMapSection? Section { get; set; }
    public int? ParcelNumber { get; set; }

    public DateTimeOffset AssignedAt { get; set; }
    /// <summary>The property transaction (a subdivision's lot, a consolidation's result) that gave it, if any (step 10a-3).</summary>
    public Guid? AssignedByTransactionId { get; set; }
    public DateTimeOffset? RetiredAt { get; set; }
    public string? RetirementReason { get; set; }
    /// <summary>The property transaction (subdivision, consolidation …) that retired it, if any (step 10a-3).</summary>
    public Guid? PropertyTransactionId { get; set; }

    /// <summary>
    /// A temporary PIN's tie-up (MRPAAO Ch. II §2 A, p.55; step 10a-5): the office links
    /// the FAAS to its parcel on the base map (the left check mark on the pre-TMCR),
    /// then the tax mapping team confirms it in the field (the right check mark).
    /// </summary>
    public DateTimeOffset? OfficeTieUpAt { get; set; }
    public Guid? OfficeTieUpBy { get; set; }
    public DateTimeOffset? FieldConfirmedAt { get; set; }
    public Guid? FieldConfirmedBy { get; set; }
    public string? TieUpRemarks { get; set; }
}
