using Prime.Domain.Common;
using Prime.Domain.Entities.Reference;
using Prime.Domain.Enums;

namespace Prime.Domain.Entities;

/// <summary>
/// The rate of a levy the LGU's ordinance sets, for the collectibles the QRRPA reports (LAM 2025 Book I p.24;
/// docs/analysis/reporting.md §4.4, §10, Q5, Q18). Configuration, effective-dated, approved by a second user, with the
/// ordinance as its legal basis; the rates are LGU data, never code (CLAUDE.md §7, §117). Versioned by <see cref="Code"/>,
/// which the keys give, so a new rate for the same levy, municipality and classification takes over from the old one.
/// Not the frozen billing rates (CLAUDE.md §0): nothing is billed from it.
/// </summary>
public sealed class LevyRate : EffectiveDatedConfiguration
{
    /// <summary>The keys as one value (see <see cref="CodeFor"/>); never typed.</summary>
    public string Code { get; set; } = string.Empty;
    public LevyKind Kind { get; set; }
    /// <summary>Null: the whole province (every municipality without a rate of its own).</summary>
    public Guid? MunicipalityId { get; set; }
    public Municipality? Municipality { get; set; }
    /// <summary>Null: every classification without a rate of its own.</summary>
    public Guid? ClassificationId { get; set; }
    public Classification? Classification { get; set; }
    /// <summary>Percent of assessed value, as the ordinance states it.</summary>
    public decimal RatePercent { get; set; }
    public string? Description { get; set; }

    public static string CodeFor(LevyKind kind, Guid? municipalityId, Guid? classificationId) =>
        $"{kind}:{municipalityId?.ToString("N") ?? "*"}:{classificationId?.ToString("N") ?? "*"}";
}
