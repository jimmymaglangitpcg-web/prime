namespace Prime.Domain.Enums;

/// <summary>
/// How an SMV adjustment factor finds its percentage for a land (docs/analysis/valuation-foundation.md
/// §4.4; LAM Bk III pp.76–78). The kinds are code; every percentage is the SMV's data.
/// </summary>
public enum AdjustmentRuleKind
{
    /// <summary>The factor's own signed percentage, chosen by the appraiser.</summary>
    Flat = 0,

    /// <summary>A percentage per kind of road, taken from the land's road type.</summary>
    ByRoadType = 1,

    /// <summary>A percentage per distance band (over … up to … km) to a named reference.</summary>
    ByDistance = 2,

    /// <summary>The factor's percentage, for a land recorded as a corner lot.</summary>
    Corner = 3,

    /// <summary>A percentage per depth band, for strips marked with their band; never on subdivision lots.</summary>
    Depth = 4,
}

/// <summary>What a <see cref="AdjustmentRuleKind.ByDistance"/> factor measures to.</summary>
public enum DistanceReference
{
    AllWeatherRoad = 0,
    Poblacion = 1,
}
