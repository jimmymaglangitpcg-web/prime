using Prime.Domain.Common;

namespace Prime.Domain.Entities.Reference;

/// <summary>CLAUDE.md §27 "Barangay".</summary>
public sealed class Barangay : AuditableEntity
{
    public Guid MunicipalityId { get; set; }
    public Municipality? Municipality { get; set; }
    /// <summary>The city district the barangay lies in, for a city or Metro Manila municipality (MRPAAO Ch. II §1 A.2b).</summary>
    public Guid? CityDistrictId { get; set; }
    public CityDistrict? CityDistrict { get; set; }
    public string PsgcCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// The assessor's 4-digit barangay index number, the PIN's 6th–9th digits (MRPAAO
    /// Ch. II §1 A.3). Unique within the municipality or district and never reused: a
    /// barangay that is divided keeps its number as retired, and the new barangays take
    /// the next numbers (§1 D.5).
    /// </summary>
    public string? PinIndexNumber { get; set; }
    /// <summary>Set when the barangay was divided (§1 D.5); a retired barangay takes no new PINs.</summary>
    public DateOnly? RetiredOn { get; set; }
    public string? RetirementReason { get; set; }
    /// <summary>For a barangay created by dividing another: the mother barangay.</summary>
    public Guid? SplitFromBarangayId { get; set; }
}

/// <summary>
/// A district of a city or Metro Manila municipality, numbered "01" upward in an
/// inverted "S" from the upper-left district (MRPAAO Ch. II §1 A.2b). Its 2-digit
/// index number is the PIN's 4th–5th digits for the barangays in it.
/// </summary>
public sealed class CityDistrict : AuditableEntity
{
    public Guid MunicipalityId { get; set; }
    public Municipality? Municipality { get; set; }
    public string IndexNumber { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}
