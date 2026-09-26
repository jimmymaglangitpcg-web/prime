using Prime.Domain.Common;

namespace Prime.Domain.Entities.Reference;

/// <summary>
/// CLAUDE.md §27 "Province". <see cref="PsgcCode"/> is the official
/// Philippine Standard Geographic Code from the Philippine Statistics
/// Authority — a public structural identifier (not a legal/tax value), safe
/// to include without domain-verification per CLAUDE.md §5/§6.
/// </summary>
public sealed class Province : AuditableEntity
{
    public string PsgcCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// The assessor's 3-digit index number, the PIN's 1st–3rd digits (MRPAAO Ch. II §1 A.1;
    /// docs/analysis/property-identification.md). Not the PSGC code. Entered by the LGU.
    /// </summary>
    public string? PinIndexNumber { get; set; }
}
