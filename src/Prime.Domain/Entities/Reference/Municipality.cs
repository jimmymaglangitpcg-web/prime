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

    /// <summary>
    /// The assessor's index number (MRPAAO Ch. II §1 A.1–A.2): 3 digits for a city or
    /// Metro Manila municipality with its own number (the PIN's 1st–3rd digits, its
    /// barangays then belong to <see cref="CityDistrict"/>s), otherwise 2 digits — the
    /// municipality's number within its province (4th–5th digits). Entered by the LGU.
    /// </summary>
    public string? PinIndexNumber { get; set; }
}
