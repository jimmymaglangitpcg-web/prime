using System.Diagnostics;
using System.Net;
using System.Text.Json;
using Prime.Domain.Exceptions;
using Prime.WebApi.Contracts;

namespace Prime.WebApi.Middleware;

/// <summary>
/// Translates unhandled exceptions into the CLAUDE.md §63 error shape and
/// strips stack traces / internal details from the response (§67/§106).
/// Domain exceptions map to 400 with their own code; anything else maps to
/// 500 with a generic message — the real exception is logged, not returned.
/// </summary>
public class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (DomainException ex)
        {
            logger.LogWarning(ex, "Domain rule violation: {Code}", ex.Code);
            await WriteErrorAsync(context, HttpStatusCode.BadRequest, ex.Code, ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception");
            await WriteErrorAsync(context, HttpStatusCode.InternalServerError, "INTERNAL_ERROR", "An unexpected error occurred.");
        }
    }

    private static async Task WriteErrorAsync(HttpContext context, HttpStatusCode statusCode, string code, string message)
    {
        var traceId = Activity.Current?.Id ?? context.TraceIdentifier;
        var error = new ApiError(code, message, Details: null, traceId);

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)statusCode;
        await context.Response.WriteAsync(JsonSerializer.Serialize(error));
    }
}
