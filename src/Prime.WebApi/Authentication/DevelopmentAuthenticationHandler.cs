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

    /// <summary>Development only: "checker" acts as the second development user.</summary>
    public const string ActAsHeader = "X-Prime-Dev-Act-As";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Reachable only where this whole bypass is (Development + DevAuth:Enabled; Program.cs).
        var checker = string.Equals(Request.Headers[ActAsHeader], "checker", StringComparison.OrdinalIgnoreCase);
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, checker ? Options.CheckerUserId : Options.UserId),
            new(ClaimTypes.Name, checker ? Options.CheckerDisplayName : Options.DisplayName),
        };
        claims.AddRange(Options.Roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
