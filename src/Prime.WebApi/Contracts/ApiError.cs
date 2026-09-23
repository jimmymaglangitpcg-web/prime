namespace Prime.WebApi.Contracts;

/// <summary>
/// Uniform API error shape mandated by CLAUDE.md §63. Every error response
/// from Prime.WebApi uses this shape — never a raw stack trace or
/// framework-default ProblemDetails body.
/// </summary>
public sealed record ApiError(string Code, string Message, object? Details, string TraceId);
