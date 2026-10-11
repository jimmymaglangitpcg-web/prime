using Prime.Domain.Common;

namespace Prime.Domain.Entities;

/// <summary>
/// The rows of a report whose layout groups the FAAS by rows an issuance fixes, and which PRIME records each row
/// gathers (docs/analysis/reporting.md §10, Q15): for the QRRPA, the classification rows of taxable properties, the
/// kinds of exemption, and the groups of properties with restrictions. Configuration: loaded from the content pack or
/// entered, effective-dated, approved by a second user; versioned by <see cref="Code"/> (the report's code). The row
/// list is LAM content, never code (CLAUDE.md §118); the repository ships a DEMO map only.
/// </summary>
public sealed class ReportRowMap : EffectiveDatedConfiguration
{
    /// <summary>The report the map is for (e.g. "QRRPA").</summary>
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    /// <summary>The rows and restriction groups, as JSON (Prime.Application's <c>ReportRowMapDefinition</c>).</summary>
    public string Definition { get; set; } = "{}";
}

/// <summary>
/// A dated figure a report or rule needs that an issuance or ordinance sets, e.g. the market value at which the QRRPA
/// splits residential buildings (docs/analysis/reporting.md §10, Q16). The codes PRIME reads are listed in code
/// (<c>SystemParameterCatalog</c>); their values are entered by the office, with their legal basis, and approved by a
/// second user. Versioned by <see cref="Code"/>.
/// </summary>
public sealed class SystemParameter : EffectiveDatedConfiguration
{
    public string Code { get; set; } = string.Empty;
    public decimal Value { get; set; }
    public string? Description { get; set; }
}
