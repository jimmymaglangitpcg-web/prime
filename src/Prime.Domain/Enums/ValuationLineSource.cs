namespace Prime.Domain.Enums;

/// <summary>
/// What one valuation line values — one FAAS appraisal row
/// (docs/analysis/mrpaao-forms-model.md §8.2). Later parts of step 2 add
/// land strips, land improvements and building use portions.
/// </summary>
public enum ValuationLineSource
{
    /// <summary>A land record with no strips, valued from its own fields.</summary>
    Land = 0,
    Building = 1,
    Machinery = 2,
    LandStrip = 3,
    LandImprovement = 4,
    BuildingUsePortion = 5,
}
