using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Prime.WebApi.Authentication;

public class DevelopmentAuthOptions : AuthenticationSchemeOptions
{
    /// <summary>Fake Supabase user id to simulate. Configurable so different roles can be tested locally.</summary>
    public string UserId { get; set; } = "00000000-0000-0000-0000-000000000001";
    public string DisplayName { get; set; } = "Local Dev User";
    public string[] Roles { get; set; } = ["SYSTEM_ADMIN"];

    /// <summary>
    /// The second development user a request selects with <see cref="DevelopmentAuthenticationHandler.ActAsHeader"/>
    /// = "checker", so maker-checker approvals can be tried on screen (docs/analysis/value-and-assess.md §4).
    /// </summary>
    public string CheckerUserId { get; set; } = "00000000-0000-0000-0000-000000000002";
    public string CheckerDisplayName { get; set; } = "Local Dev Checker";

    /// <summary>
    /// Named DEMO users a request selects with <see cref="DevelopmentAuthenticationHandler.ActAsHeader"/> = key
    /// (docs/analysis/province-wide-operation.md Q13). <c>DevOfficeSeeder</c> gives each one its office and roles.
    /// Empty (the usual case, since appsettings.*.json is not in git) means <see cref="DefaultUsers"/>.
    /// </summary>
    public List<DevelopmentUser> Users { get; set; } = [];

    public IReadOnlyList<DevelopmentUser> EffectiveUsers => Users.Count > 0 ? Users : DefaultUsers;

    public static readonly IReadOnlyList<DevelopmentUser> DefaultUsers =
    [
        // The usual dev user prepares everything; the checker (an assessor) approves (docs/analysis/workflow-security.md §4.1).
        new() { Key = "admin", UserId = "00000000-0000-0000-0000-000000000001", DisplayName = "Local Dev User", Office = "province-wide",
            Roles = ["SYSTEM_ADMIN", "APPRAISER", "ASSESSMENT_ENCODER", "GIS_OFFICER", "REPORTING_OFFICER"] },
        new() { Key = "checker", UserId = "00000000-0000-0000-0000-000000000002", DisplayName = "Local Dev Checker", Office = "provincial", Roles = ["ASSESSOR"] },
        new() { Key = "mun-appraiser", UserId = "00000000-0000-0000-0000-000000000003", DisplayName = "DEMO Municipal Appraiser", Office = "municipal", Roles = ["APPRAISER"] },
        new() { Key = "mun-assessor", UserId = "00000000-0000-0000-0000-000000000004", DisplayName = "DEMO Municipal Assessor", Office = "municipal", Roles = ["ASSESSOR"] },
        // Signed up but not yet approved: tries the sign-up screens (docs/analysis/workflow-security.md §4.2).
        new() { Key = "applicant", UserId = "00000000-0000-0000-0000-000000000005", DisplayName = "DEMO Applicant", Office = "none", Roles = [] },
        // VIEW_ONLY: sees an individual taxpayer's personal data masked (workflow-security.md Q16).
        new() { Key = "viewer", UserId = "00000000-0000-0000-0000-000000000006", DisplayName = "DEMO Viewer", Office = "provincial", Roles = ["VIEW_ONLY"] },
    ];
}

/// <param name="Office">
/// "province-wide", "provincial", "municipal" (the first DEMO municipal office), or "none": no office and no account
/// set up, so the user signs in as a new, pending applicant.
/// </param>
public sealed class DevelopmentUser
{
    public string Key { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Office { get; set; } = "provincial";
    public string[] Roles { get; set; } = [];
}

/// <summary>
/// Simulates an authenticated Supabase identity so business-logic and UI
/// development is not blocked by needing a live Supabase project on every
/// developer machine (see docs/ARCHITECTURE.md §3.4). Registered ONLY in
/// the Development environment (see Program.cs) — Staging/Production always
/// validate real Supabase-issued JWTs via the standard JwtBearer handler.
/// This handler must never be reachable outside Development: it grants
/// access to anyone who can reach the API, by design, for local convenience
/// only.
/// </summary>
public class DevelopmentAuthenticationHandler(
    IOptionsMonitor<DevelopmentAuthOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<DevelopmentAuthOptions>(options, logger, encoder)
{
    public const string SchemeName = "DevelopmentBypass";

    /// <summary>Development only: the key of a configured DEMO user to act as (Q13); "checker" still selects the second user.</summary>
    public const string ActAsHeader = "X-Prime-Dev-Act-As";

    /// <summary>
    /// Development only: the token's assurance level to simulate ("aal1" = password only). Without it the DEMO user
    /// counts as signed in with a second factor (aal2), so MFA-required roles work locally (workflow-security.md Q7).
    /// </summary>
    public const string AssuranceLevelHeader = "X-Prime-Dev-Aal";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Reachable only where this whole bypass is (Development + DevAuth:Enabled; Program.cs).
        var key = Request.Headers[ActAsHeader].ToString();
        var named = Options.EffectiveUsers.FirstOrDefault(u => string.Equals(u.Key, key, StringComparison.OrdinalIgnoreCase));
        var checker = named is null && string.Equals(key, "checker", StringComparison.OrdinalIgnoreCase);
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, named?.UserId ?? (checker ? Options.CheckerUserId : Options.UserId)),
            new(ClaimTypes.Name, named?.DisplayName ?? (checker ? Options.CheckerDisplayName : Options.DisplayName)),
        };
        claims.AddRange(Options.Roles.Select(role => new Claim(ClaimTypes.Role, role)));
        var aal = Request.Headers[AssuranceLevelHeader].ToString();
        claims.Add(new Claim("aal", aal is "aal1" or "aal2" ? aal : "aal2"));

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
