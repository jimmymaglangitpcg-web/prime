using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Prime.Application.Common;
using Prime.WebApi.Contracts;

namespace Prime.WebApi.Controllers;

/// <summary>
/// Maps Application-layer <see cref="Result"/>/<see cref="Result{T}"/>
/// outcomes to HTTP responses in the uniform CLAUDE.md §63 shape. Every
/// PRIME API controller inherits this instead of hand-rolling status-code
/// mapping per action.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public abstract class ApiControllerBase : ControllerBase
{
    protected ActionResult<T> HandleResult<T>(Result<T> result) =>
        result.IsSuccess ? Ok(result.Value) : ToErrorResult<T>(result);

    protected ActionResult<T> HandleCreated<T>(Result<T> result, string actionName, Func<T, object> routeValues) =>
        result.IsSuccess ? CreatedAtAction(actionName, routeValues(result.Value), result.Value) : ToErrorResult<T>(result);

    /// <summary>
    /// Refuses an empty upload or one whose file name lacks an allowed extension (docs/analysis/workflow-security.md
    /// §4.4). The content is still checked by the service that reads it; the declared content type is not trusted.
    /// </summary>
    protected static Result CheckUpload(IFormFile? file, params string[] extensions) =>
        file is null || file.Length == 0
            ? Result.Failure("UPLOAD_EMPTY", "Choose a file to upload.")
            : extensions.Any(e => file.FileName.EndsWith(e, StringComparison.OrdinalIgnoreCase))
                ? Result.Success()
                : Result.Failure("UPLOAD_TYPE_INVALID", $"The file must be a {string.Join(" or ", extensions)} file.");

    private ActionResult<T> ToErrorResult<T>(Result result)
    {
        var statusCode = result.Code switch
        {
            not null when result.Code.EndsWith("_NOT_FOUND") => StatusCodes.Status404NotFound,
            // Outside the user's jurisdiction (docs/analysis/province-wide-operation.md §3.3).
            not null when result.Code.EndsWith("_FORBIDDEN") => StatusCodes.Status403Forbidden,
            not null when result.Code.EndsWith("_DUPLICATE") => StatusCodes.Status409Conflict,
            not null when result.Code.Contains("EXCEEDS") => StatusCodes.Status409Conflict,
            not null when result.Code.EndsWith("_CONFLICT") => StatusCodes.Status409Conflict,
            // Collection: the state changed under the cashier (docs/analysis/collection.md §3).
            not null when result.Code.EndsWith("_ALREADY_SETTLED") || result.Code.EndsWith("_QUOTE_CHANGED") => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status400BadRequest,
        };

        var error = new ApiError(result.Code!, result.Message ?? "The request could not be completed.", null, Activity.Current?.Id ?? HttpContext.TraceIdentifier);
        return StatusCode(statusCode, error);
    }
}
