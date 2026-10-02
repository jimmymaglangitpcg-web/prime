namespace Prime.Domain.Enums;

/// <summary>Fixed set per CLAUDE.md §22.</summary>
public enum RpuType
{
    Land = 0,
    Building = 1,
    Machinery = 2,
    OtherImprovement = 3,
    /// <summary>
    /// A mineral right held apart from the surface: the surface PIN with its own series (LAM Bk III pp.59–60;
    /// docs/analysis/identification-numbering.md §4.2). Its valuation is not built yet (Q8).
    /// </summary>
    MineralRight = 4,
}
