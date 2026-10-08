using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Prime.Infrastructure.Identity;

/// <summary>
/// Checks the Supabase Auth project settings PRIME relies on that the project publishes (<c>/auth/v1/settings</c>;
/// docs/analysis/workflow-security.md §4.2 Q8): sign-in by e-mail only, e-mail confirmation required, no anonymous
/// sign-ins. Password length, leaked-password checks, lockout and token lifetime are not published; they are listed in
/// docs/SECURITY.md and checked by hand. Needs <c>Supabase:Url</c> and the project's public anon key
/// (<c>Supabase:AnonKey</c>, not a secret).
/// </summary>
public sealed class SupabaseAuthSettingsCheck(IHttpClientFactory httpClients, IConfiguration configuration) : IHealthCheck
{
    public const string HttpClientName = "supabase-auth";

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var url = configuration["Supabase:Url"];
        var anonKey = configuration["Supabase:AnonKey"];
        if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(anonKey))
        {
            return HealthCheckResult.Degraded("Supabase:Url or Supabase:AnonKey is not configured; the Auth settings were not checked.");
        }
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{url.TrimEnd('/')}/auth/v1/settings");
            request.Headers.Add("apikey", anonKey);
            using var response = await httpClients.CreateClient(HttpClientName).SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();
            var settings = await response.Content.ReadFromJsonAsync<JsonObject>(cancellationToken) ?? [];
            var problems = Problems(settings);
            return problems.Count == 0
                ? HealthCheckResult.Healthy("Supabase Auth: e-mail sign-in with confirmation; no anonymous or other providers.")
                : HealthCheckResult.Degraded($"Supabase Auth settings differ from docs/SECURITY.md: {string.Join("; ", problems)}.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Degraded("Supabase Auth settings could not be read.", ex);
        }
    }

    /// <summary>What differs from PRIME's required settings; empty when none.</summary>
    public static IReadOnlyList<string> Problems(JsonObject settings)
    {
        var problems = new List<string>();
        var external = settings["external"] as JsonObject ?? [];
        if (external["email"]?.GetValue<bool>() != true)
        {
            problems.Add("e-mail sign-in is off");
        }
        if (settings["mailer_autoconfirm"]?.GetValue<bool>() != false)
        {
            problems.Add("e-mail addresses are not confirmed (mailer_autoconfirm)");
        }
        if (external["anonymous_users"]?.GetValue<bool>() == true)
        {
            problems.Add("anonymous sign-ins are on");
        }
        var others = external.Where(p => p.Key is not ("email" or "anonymous_users") && p.Value?.GetValueKind() == System.Text.Json.JsonValueKind.True)
            .Select(p => p.Key).ToList();
        if (others.Count > 0)
        {
            problems.Add($"other sign-in providers are on ({string.Join(", ", others)})");
        }
        return problems;
    }
}

/// <summary>Runs <see cref="SupabaseAuthSettingsCheck"/> once at start-up and logs what differs; it never stops the API.</summary>
public sealed class SupabaseAuthSettingsStartupCheck(SupabaseAuthSettingsCheck check, ILogger<SupabaseAuthSettingsStartupCheck> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var result = await check.CheckHealthAsync(new HealthCheckContext(), stoppingToken);
        if (result.Status == HealthStatus.Healthy)
        {
            logger.LogInformation("{Description}", result.Description);
        }
        else
        {
            logger.LogWarning(result.Exception, "{Description}", result.Description);
        }
    }
}
