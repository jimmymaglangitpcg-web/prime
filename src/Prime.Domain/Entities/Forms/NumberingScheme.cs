using Prime.Domain.Common;
using Prime.Domain.Enums;

namespace Prime.Domain.Entities.Forms;

/// <summary>
/// How numbers of one <see cref="NumberedDocumentKind"/> are formed
/// (docs/FORMS-REVISION-PLAN.md §4.4). <see cref="Pattern"/> uses the tokens
/// understood by <c>NumberPattern</c>. With no scheme in force, numbers are
/// entered by hand as before; a scheme applies only from its effective date
/// and never renumbers existing records. One scheme in force per kind.
/// </summary>
public sealed class NumberingScheme : EffectiveDatedConfiguration
{
    public NumberedDocumentKind AppliesTo { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Pattern { get; set; } = string.Empty;
    /// <summary>Optional extra check for numbers typed by hand.</summary>
    public string? ValidationRegex { get; set; }
    /// <summary>When true, a user may type a number instead of generating one (it must still match <see cref="ValidationRegex"/>).</summary>
    public bool AllowManualEntry { get; set; }
}

/// <summary>
/// The last number used for one scheme and scope (the pattern rendered
/// without its sequence token, e.g. "TD-2026-"). Advanced atomically inside
/// the caller's transaction, so a failed issuance does not consume a number.
/// </summary>
public sealed class NumberSequence : Entity
{
    public Guid NumberingSchemeId { get; set; }
    public string ScopeKey { get; set; } = string.Empty;
    public long LastValue { get; set; }
}
