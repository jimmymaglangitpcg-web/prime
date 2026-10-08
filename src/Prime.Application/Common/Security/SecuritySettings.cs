namespace Prime.Application.Common.Security;

/// <summary>
/// Sign-in settings (the <c>Security</c> section; docs/analysis/workflow-security.md §4.2, Q7, Q9). Bound once at
/// start-up by the API.
/// </summary>
public sealed class SecuritySettings
{
    /// <summary>
    /// Roles whose holders must have signed in with a second factor (token assurance level aal2) to use PRIME; unset
    /// means <see cref="DefaultMfaRequiredRoles"/>, an empty list none. Not initialised: the configuration binder appends
    /// to an existing array, so a default here could never be narrowed.
    /// </summary>
    public string[]? MfaRequiredRoles { get; set; }

    public static readonly string[] DefaultMfaRequiredRoles = ["SYSTEM_ADMIN", "ASSESSOR"];

    /// <summary>Minutes without activity after which the browser signs the user out.</summary>
    public int IdleMinutes { get; set; } = 30;

    /// <summary>The token's authenticator assurance level after a second factor (Supabase Auth's <c>aal</c> claim).</summary>
    public const string MfaAssuranceLevel = "aal2";

    public bool RequiresMfa(IEnumerable<string> roles) =>
        roles.Any(r => (MfaRequiredRoles ?? DefaultMfaRequiredRoles).Contains(r, StringComparer.Ordinal));

    /// <summary>True when the roles need no second factor or the token carries one.</summary>
    public bool MfaSatisfied(IEnumerable<string> roles, string? assuranceLevel) =>
        !RequiresMfa(roles) || string.Equals(assuranceLevel, MfaAssuranceLevel, StringComparison.Ordinal);
}
