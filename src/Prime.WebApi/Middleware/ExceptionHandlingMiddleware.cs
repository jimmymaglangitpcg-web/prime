using System.Diagnostics;
using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Prime.Domain.Exceptions;
using Prime.WebApi.Contracts;

namespace Prime.WebApi.Middleware;

/// <summary>
/// Translates unhandled exceptions into the CLAUDE.md §63 error shape and
/// strips stack traces / internal details from the response (§67/§106).
/// Domain exceptions map to 400 with their own code; a concurrency conflict
/// to 409 CONCURRENCY_CONFLICT; anything else maps to
/// 500 with a generic message — the real exception is logged, not returned.
/// </summary>
public class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    // camelCase, as the controllers write ApiError (CLAUDE.md §63) and the frontend reads it.
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

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
        catch (DbUpdateConcurrencyException ex)
        {
            // A row version or If-Match mismatch (docs/analysis/production-hardening.md §4.4): nothing was saved.
            logger.LogWarning(ex, "Concurrency conflict on {Path}", context.Request.Path);
            await WriteErrorAsync(context, HttpStatusCode.Conflict, "CONCURRENCY_CONFLICT",
                "This record was changed by someone else since it was loaded. Nothing was saved; reload it and try again.");
        }
        catch (Exception ex) when (ex is OverflowException || ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.NumericValueOutOfRange })
        {
            // An amount beyond decimal or numeric(18,2) (production-hardening.md §9, H3): a mistyped input, not a server fault.
            logger.LogWarning(ex, "Value out of range on {Path}", context.Request.Path);
            await WriteErrorAsync(context, HttpStatusCode.BadRequest, ValueOutOfRangeException.ErrorCode,
                "A computed or entered amount is larger than PRIME can store. Nothing was saved; check the areas, quantities, costs and rates entered.");
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
        await context.Response.WriteAsync(JsonSerializer.Serialize(error, JsonOptions));
    }
}
