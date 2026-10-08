namespace Prime.WebApi.Security;

/// <summary>
/// Security headers on every API response outside Development (docs/analysis/workflow-security.md §4.4). The API
/// returns JSON only, so its policy forbids everything, including framing; issued forms are HTML inside JSON and render
/// under the SPA's own policy and the renderer's meta policy. HSTS is sent on HTTPS responses only, as the standard
/// requires. The SPA host's headers are set by its web server (docs/SECURITY.md).
/// </summary>
public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    public const string HstsValue = "max-age=31536000; includeSubDomains";
    public const string ContentSecurityPolicyValue = "default-src 'none'; frame-ancestors 'none'";

    public Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;
            headers.XContentTypeOptions = "nosniff";
            headers["Referrer-Policy"] = "no-referrer";
            headers.ContentSecurityPolicy = ContentSecurityPolicyValue;
            headers.XFrameOptions = "DENY";
            if (context.Request.IsHttps)
            {
                headers.StrictTransportSecurity = HstsValue;
            }
            return Task.CompletedTask;
        });
        return next(context);
    }
}
