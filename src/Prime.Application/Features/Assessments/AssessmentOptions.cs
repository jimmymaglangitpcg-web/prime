using Prime.Domain.Enums;

namespace Prime.Application.Features.Assessments;

/// <summary>Assessment policy settings (docs/analysis/mrpaao-forms-model.md §8.2); DOMAIN VERIFICATION REQUIRED.</summary>
public sealed class AssessmentOptions
{
    public const string SectionName = "Assessment";

    /// <summary>The market value a level bracket is looked up with: each line's own (default) or the unit's total.</summary>
    public LevelBracketBasis LevelBracketBasis { get; set; } = LevelBracketBasis.Line;
}
