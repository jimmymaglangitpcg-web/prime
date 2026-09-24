using System.Text.Json.Nodes;

namespace Prime.Application.Common.Interfaces;

/// <summary>
/// Advances a numbering sequence atomically (docs/FORMS-REVISION-PLAN.md
/// §4.4) and returns the new value, starting at 1. Runs inside the caller's
/// current transaction, so a rolled-back issuance does not consume a number.
/// </summary>
public interface INumberSequenceAllocator
{
    Task<long> NextAsync(Guid numberingSchemeId, string scopeKey, CancellationToken cancellationToken = default);
}

/// <summary>
/// Turns a form template and its data snapshot into a complete, printable
/// HTML document (docs/FORMS-REVISION-PLAN.md §4.2). Output values are
/// HTML-encoded and the document forbids scripts. A provisional form always
/// carries a watermark added outside the template.
/// </summary>
public interface IFormRenderer
{
    /// <summary>A parse error in <paramref name="templateBody"/>, or null when it is valid.</summary>
    string? Validate(string templateBody);

    string Render(string templateBody, JsonObject data, bool provisional);
}
