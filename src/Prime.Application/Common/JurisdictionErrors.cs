namespace Prime.Application.Common;

/// <summary>The refusal for writing outside the user's jurisdiction (docs/analysis/province-wide-operation.md §3.3); maps to HTTP 403.</summary>
public static class JurisdictionErrors
{
    public const string Code = "JURISDICTION_FORBIDDEN";
    public const string Message = "That city/municipality is outside your office's jurisdiction.";
}
