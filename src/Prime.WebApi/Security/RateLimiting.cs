using System.Diagnostics;
using System.Globalization;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Prime.WebApi.Contracts;

namespace Prime.WebApi.Security;

/// <summary>
/// Request limits (the <c>RateLimits</c> section; docs/analysis/workflow-security.md §4.4, Q13). Sign-in itself is
/// limited by Supabase Auth, not here.
/// </summary>
public sealed class RateLimitSettings
{
    /// <summary>Unset: on everywhere except Development, where the DEMO users and the test suite share one identity.</summary>
    public bool? Enabled { get; set; }

    /// <summary>Requests a user (or, before sign-in, an IP address) may make per window to any endpoint.</summary>
    public int GeneralPermitLimit { get; set; } = 300;

    /// <summary>Uploads, imports and exports (form issues, register runs) per window, on top of the general limit.</summary>
    public int StrictPermitLimit { get; set; } = 10;

    public int WindowSeconds { get; set; } = 60;
}

/// <summary>The API's rate limiter: a general limit on every request and a strict one on <see cref="StrictPolicy"/> endpoints.</summary>
public static class RateLimiting
{
    /// <summary>The policy name for uploads, imports and exports (<c>[EnableRateLimiting(RateLimiting.StrictPolicy)]</c>).</summary>
    public const string StrictPolicy = "strict";

    public const string RateLimited = "RATE_LIMITED";

    public static IServiceCollection AddPrimeRateLimiting(this IServiceCollection services, RateLimitSettings settings, bool enabled)
    {
        var window = TimeSpan.FromSeconds(settings.WindowSeconds);
        services.AddRateLimiter(options =>
        {
            if (enabled)
            {
                options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                    RateLimitPartition.GetFixedWindowLimiter(PartitionKey(context), _ => Window(settings.GeneralPermitLimit, window)));
            }
            // The policy must exist for the endpoints that name it; switched off, it never refuses.
            options.AddPolicy(StrictPolicy, context => enabled
                ? RateLimitPartition.GetFixedWindowLimiter(PartitionKey(context), _ => Window(settings.StrictPermitLimit, window))
                : RateLimitPartition.GetNoLimiter(string.Empty));

            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (rejected, cancellationToken) =>
            {
                var http = rejected.HttpContext;
                if (rejected.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    http.Response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
                }
                http.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(RateLimiting))
                    .LogWarning("Rate limit reached for {Partition} on {Path}", PartitionKey(http), http.Request.Path);
                await http.Response.WriteAsJsonAsync(new ApiError(RateLimited, "Too many requests. Wait a moment and try again.",
                    null, Activity.Current?.Id ?? http.TraceIdentifier), cancellationToken);
            };
        });
        return services;
    }

    /// <summary>The signed-in user's subject, else the client's address (which needs the forwarded headers behind a proxy).</summary>
    private static string PartitionKey(HttpContext context) =>
        context.User.FindFirstValue(ClaimTypes.NameIdentifier) is { Length: > 0 } subject
            ? $"user:{subject}"
            : $"ip:{context.Connection.RemoteIpAddress}";

    private static FixedWindowRateLimiterOptions Window(int permitLimit, TimeSpan window) => new()
    {
        PermitLimit = permitLimit,
        Window = window,
        QueueLimit = 0,
        AutoReplenishment = true,
    };
}
