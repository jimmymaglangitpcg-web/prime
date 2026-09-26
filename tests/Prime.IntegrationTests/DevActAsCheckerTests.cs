using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Prime.WebApi.Authentication;
using Shouldly;
using Xunit;

namespace Prime.IntegrationTests;

/// <summary>
/// docs/analysis/value-and-assess.md §4: the development-only "act as checker"
/// header selects the second development user; without it, the usual one.
/// </summary>
public class DevActAsCheckerTests
{
    private static async Task<ClaimsPrincipal> AuthenticateAsync(string? actAs)
    {
        var options = new DevelopmentAuthOptions();
        var monitor = new StaticOptionsMonitor(options);
        var handler = new DevelopmentAuthenticationHandler(monitor, NullLoggerFactory.Instance, UrlEncoder.Default);
        var context = new DefaultHttpContext();
        if (actAs is not null)
        {
            context.Request.Headers[DevelopmentAuthenticationHandler.ActAsHeader] = actAs;
        }
        await handler.InitializeAsync(
            new AuthenticationScheme(DevelopmentAuthenticationHandler.SchemeName, null, typeof(DevelopmentAuthenticationHandler)), context);
        var result = await handler.AuthenticateAsync();
        result.Succeeded.ShouldBeTrue();
        return result.Principal!;
    }

    [Theory]
    [InlineData(null, "00000000-0000-0000-0000-000000000001", "Local Dev User")]
    [InlineData("checker", "00000000-0000-0000-0000-000000000002", "Local Dev Checker")]
    [InlineData("CHECKER", "00000000-0000-0000-0000-000000000002", "Local Dev Checker")]
    [InlineData("admin", "00000000-0000-0000-0000-000000000001", "Local Dev User")] // only "checker" switches
    public async Task Header_SelectsTheDevelopmentUser(string? actAs, string userId, string name)
    {
        var user = await AuthenticateAsync(actAs);

        user.FindFirstValue(ClaimTypes.NameIdentifier).ShouldBe(userId);
        user.FindFirstValue(ClaimTypes.Name).ShouldBe(name);
    }

    private sealed class StaticOptionsMonitor(DevelopmentAuthOptions value) : IOptionsMonitor<DevelopmentAuthOptions>
    {
        public DevelopmentAuthOptions CurrentValue => value;
        public DevelopmentAuthOptions Get(string? name) => value;
        public IDisposable? OnChange(Action<DevelopmentAuthOptions, string?> listener) => null;
    }
}
