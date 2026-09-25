namespace Prime.Domain.Enums;

/// <summary>
/// The market value an assessment level's bracket is looked up with
/// (docs/analysis/mrpaao-forms-model.md §8.2). DOMAIN VERIFICATION REQUIRED.
/// </summary>
public enum LevelBracketBasis
{
    /// <summary>Each assessment line's own market value.</summary>
    Line = 0,
    /// <summary>The unit's total market value, for every line.</summary>
    Unit = 1,
}
