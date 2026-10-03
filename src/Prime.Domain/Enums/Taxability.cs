namespace Prime.Domain.Enums;

/// <summary>
/// CLAUDE.md §23. An assessment line is <see cref="Taxable"/> or <see cref="Exempt"/>; a Tax Declaration's taxability
/// summarises its lines, <see cref="PartlyExempt"/> when it has both (docs/analysis/assessment-listing-exemptions.md, Q2).
/// </summary>
public enum Taxability
{
    Taxable = 0,
    Exempt = 1,
    /// <summary>A Tax Declaration with taxable and exempt lines; never a line's own value.</summary>
    PartlyExempt = 2,
}
