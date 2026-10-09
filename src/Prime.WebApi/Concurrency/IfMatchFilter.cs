using System.Diagnostics;
using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Prime.Application.Common.Interfaces;
using Prime.WebApi.Contracts;

namespace Prime.WebApi.Concurrency;

/// <summary>
/// Reads <c>If-Match</c> on a write to a record route (<c>{id}</c>) into the request's
/// <see cref="IConcurrencyExpectation"/> (docs/analysis/production-hardening.md §4.4). The value is the record's
/// <c>version</c> as the client displayed it, bare or as an entity tag (<c>"42"</c>, <c>W/"42"</c>); <c>*</c> and an
/// absent header check nothing. A malformed value is refused with 400 rather than ignored.
/// </summary>
public sealed class IfMatchFilter : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var request = context.HttpContext.Request;
        var header = request.Headers.IfMatch.ToString().Trim();
        if (header.Length > 0 && header != "*" && !HttpMethods.IsGet(request.Method) && !HttpMethods.IsHead(request.Method))
        {
            if (!TryParse(header, out var version))
            {
                context.Result = new BadRequestObjectResult(new ApiError("INVALID_IF_MATCH",
                    "If-Match must carry the record's version, e.g. \"42\".", null, Activity.Current?.Id ?? context.HttpContext.TraceIdentifier));
                return;
            }
            if (context.RouteData.Values.TryGetValue("id", out var raw) && Guid.TryParse(raw?.ToString(), out var id))
            {
                context.HttpContext.RequestServices.GetRequiredService<IConcurrencyExpectation>().Expect(id, version);
            }
        }
        await next();
    }

    public static bool TryParse(string header, out uint version)
    {
        var value = header.StartsWith("W/", StringComparison.Ordinal) ? header[2..] : header;
        if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
        {
            value = value[1..^1];
        }
        return uint.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out version);
    }
}
