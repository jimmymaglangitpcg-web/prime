using Prime.Domain.Common;

namespace Prime.Domain.Entities.Reference;

/// <summary>
/// A tax map section of a barangay (MRPAAO Ch. II §1 A.4; §2): one tax map sheet,
/// numbered "001" upward in an inverted "S" from the upper-left section. Its 3-digit
/// index number is the PIN's 10th–12th digits. Numbers are never reused: a retired
/// section keeps its number. A crowded subdivision is drawn on a new section, which
/// records the section it came from (§2 E.2). Its boundary is a GIS layer (step 10a-4).
/// </summary>
public sealed class TaxMapSection : AuditableEntity
{
    public Guid BarangayId { get; set; }
    public Barangay? Barangay { get; set; }
    public string IndexNumber { get; set; } = string.Empty;
    public string? Remarks { get; set; }
    public Guid? SplitFromSectionId { get; set; }
    public DateOnly? RetiredOn { get; set; }
    public string? RetirementReason { get; set; }
}
