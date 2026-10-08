using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Prime.Infrastructure.Identity;
using Prime.WebApi.Security;
using Shouldly;
using Xunit;

namespace Prime.IntegrationTests;

/// <summary>
/// Phase 12 step P12-4 (docs/analysis/workflow-security.md §4.4): requests over the limits get 429 in the §63 shape;
/// responses carry the security headers outside Development (HSTS on HTTPS only); uploads of the wrong type are refused
/// before they are read; with real authentication a cookie, or the Development act-as header, never signs anyone in.
/// Nothing is saved.
/// </summary>
public class SecurityHardeningTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    // UseSetting, not ConfigureAppConfiguration: Program reads these while building the services.
    private WebApplicationFactory<Program> With(params (string Key, string Value)[] settings) =>
        factory.WithWebHostBuilder(b =>
        {
            foreach (var (key, value) in settings)
            {
                b.UseSetting(key, value);
            }
        });

    [Fact]
    public async Task Requests_over_the_general_limit_get_429_with_a_retry_after()
    {
        await using var host = With(("RateLimits:Enabled", "true"), ("RateLimits:GeneralPermitLimit", "3"), ("RateLimits:WindowSeconds", "60"));
        var client = host.CreateClient();
        for (var i = 0; i < 3; i++)
        {
            (await client.GetAsync("/api/me")).StatusCode.ShouldBe(HttpStatusCode.OK);
        }
        var refused = await client.GetAsync("/api/me");
        refused.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        refused.Headers.RetryAfter.ShouldNotBeNull();
        (await refused.Content.ReadFromJsonAsync<JsonObject>())!["code"]!.GetValue<string>().ShouldBe(RateLimiting.RateLimited);
    }

    [Fact]
    public async Task Uploads_imports_and_exports_have_their_own_stricter_limit()
    {
        await using var host = With(("RateLimits:Enabled", "true"), ("RateLimits:StrictPermitLimit", "1"));
        var client = host.CreateClient();
        // The first is refused for its content (no file) but still counts; the second is refused by the limit.
        (await client.PostAsync("/api/market-data/transactions/import/preview", Csv("x.csv", ""))).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await client.PostAsync("/api/market-data/transactions/import/preview", Csv("x.csv", ""))).StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        // The general limit is untouched.
        (await client.GetAsync("/api/me")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Limits_are_off_in_development_unless_switched_on()
    {
        await using var host = With(("RateLimits:GeneralPermitLimit", "1"));
        var client = host.CreateClient();
        (await client.GetAsync("/api/me")).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await client.GetAsync("/api/me")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Responses_carry_the_security_headers_when_switched_on_and_hsts_only_over_https()
    {
        await using var host = With(("Security:Headers", "true"));
        var https = await host.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") }).GetAsync("/api/me");
        https.StatusCode.ShouldBe(HttpStatusCode.OK);
        https.Headers.GetValues("Strict-Transport-Security").Single().ShouldBe(SecurityHeadersMiddleware.HstsValue);
        https.Headers.GetValues("X-Content-Type-Options").Single().ShouldBe("nosniff");
        https.Headers.GetValues("Referrer-Policy").Single().ShouldBe("no-referrer");
        https.Headers.GetValues("X-Frame-Options").Single().ShouldBe("DENY");
        https.Headers.GetValues("Content-Security-Policy").Single().ShouldBe(SecurityHeadersMiddleware.ContentSecurityPolicyValue);

        // Errors carry them too, and plain HTTP gets no HSTS.
        var http = await host.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }).GetAsync("/api/no-such-endpoint");
        http.Headers.Contains("X-Content-Type-Options").ShouldBeTrue();
        http.Headers.Contains("Strict-Transport-Security").ShouldBeFalse();

        // Development's default: none.
        (await factory.CreateClient().GetAsync("/api/me")).Headers.Contains("Content-Security-Policy").ShouldBeFalse();
    }

    [Fact]
    public async Task Uploads_of_the_wrong_type_are_refused()
    {
        var client = factory.CreateClient();
        var csv = await client.PostAsync("/api/market-data/transactions/import/preview", Csv("sales.xlsx", "a,b\n1,2\n"));
        csv.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await csv.Content.ReadFromJsonAsync<JsonObject>())!["code"]!.GetValue<string>().ShouldBe("UPLOAD_TYPE_INVALID");

        var pack = await client.PostAsync("/api/content-packs/upload", Csv("pack.txt", "not a zip"));
        pack.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await pack.Content.ReadFromJsonAsync<JsonObject>())!["code"]!.GetValue<string>().ShouldBe("UPLOAD_TYPE_INVALID");
    }

    [Fact]
    public async Task With_real_authentication_a_cookie_or_the_dev_header_signs_nobody_in()
    {
        // The API takes bearer tokens only, so there is no session cookie to forge across sites (CSRF does not apply).
        var connection = factory.Services.GetRequiredService<IConfiguration>().GetConnectionString("PrimeDb");
        await using var host = factory.WithWebHostBuilder(b => b
            .UseEnvironment("Production")
            .UseSetting("ConnectionStrings:PrimeDb", connection)
            .UseSetting("Supabase:Url", "https://prime-test.invalid")
            .UseSetting("DevAuth:Enabled", "true"));
        var client = host.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });

        var withCookie = new HttpRequestMessage(HttpMethod.Get, "/api/me");
        withCookie.Headers.Add("Cookie", ".AspNetCore.Cookies=forged; sb-access-token=forged");
        (await client.SendAsync(withCookie)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        var actAs = new HttpRequestMessage(HttpMethod.Get, "/api/me");
        actAs.Headers.Add("X-Prime-Dev-Act-As", "checker");
        var refused = await client.SendAsync(actAs);
        refused.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        refused.Headers.Contains("Strict-Transport-Security").ShouldBeTrue("outside Development the headers are on by default");
    }

    [Fact]
    public void The_supabase_auth_settings_check_lists_what_differs_from_the_required_settings()
    {
        static JsonObject Settings(bool email = true, bool autoconfirm = false, bool anonymous = false, bool google = false) => new()
        {
            ["external"] = new JsonObject { ["email"] = email, ["anonymous_users"] = anonymous, ["google"] = google, ["phone"] = false },
            ["disable_signup"] = false,
            ["mailer_autoconfirm"] = autoconfirm,
        };
        SupabaseAuthSettingsCheck.Problems(Settings()).ShouldBeEmpty();
        SupabaseAuthSettingsCheck.Problems(Settings(email: false, autoconfirm: true, anonymous: true, google: true)).ShouldBe([
            "e-mail sign-in is off", "e-mail addresses are not confirmed (mailer_autoconfirm)", "anonymous sign-ins are on",
            "other sign-in providers are on (google)"]);
        SupabaseAuthSettingsCheck.Problems([]).Count.ShouldBe(2); // nothing published: e-mail and confirmation unknown
    }

    private static MultipartFormDataContent Csv(string fileName, string content)
    {
        var file = new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes(content));
        file.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        return new MultipartFormDataContent { { file, "file", fileName } };
    }
}
