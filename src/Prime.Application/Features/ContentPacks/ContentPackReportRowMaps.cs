using System.Text.Json;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Prime.Application.Features.ReportConfiguration;

namespace Prime.Application.Features.ContentPacks;

/// <summary>
/// Step R4c (docs/analysis/reporting.md §10, Q15): report row maps as content, keyed by the report's code, imported as
/// Draft versions and approved by a second user. The codes a map names are checked against PRIME and the lookups the
/// same pack adds; exemption types are checked at import, when the pack's exemption types exist.
/// </summary>
public sealed partial class ContentPackVersionedContent
{
    private sealed record ReportRowMapItem(string? Code, string? Name, JsonElement? Definition, string? LegalBasis, string? EffectiveDate, string? Remarks,
        string? Source);

    private async Task<VersionedPreview> ReportRowMapsAsync(List<ReportRowMapItem> items, string? fileSource, PackPending pending,
        IValidator<CreateReportRowMapRequest> validator, VersionedPreview result, CancellationToken ct)
    {
        var existing = await db.ReportRowMaps.AsNoTracking().ToListAsync(ct);
        var unchanged = 0;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        bool Pending(string catalogue, string code) =>
            catalogue == "exemption-types" || (pending.Lookups.TryGetValue(catalogue, out var codes) && codes.Contains(code));
        for (var i = 0; i < items.Count; i++)
        {
            var (n, x) = (i + 1, items[i]);
            if (!Common(result, n, x.Source, fileSource, x.EffectiveDate, out var source, out var effective))
            {
                continue;
            }
            if (x.Definition is not { ValueKind: JsonValueKind.Object } json)
            {
                result.Issues.Add(Error("ROW_MAP_INVALID", $"Item {n}: \"definition\" must be an object with \"rows\".", null, $"[{n}].definition"));
                continue;
            }
            var request = new CreateReportRowMapRequest(Trim(x.Code) ?? "", Trim(x.Name) ?? "", json, Trim(x.LegalBasis) ?? "", effective, Trim(x.Remarks));
            if (!await ValidAsync(validator, request, result, n, ct) || !Unique(result, seen, request.Code, n, "code"))
            {
                continue;
            }
            ReportRowMapDefinition definition;
            try
            {
                definition = ReportRowMapDefinition.Parse(json.GetRawText());
            }
            catch (JsonException ex)
            {
                result.Issues.Add(Error("ROW_MAP_INVALID", $"Item {n}: the definition is not a valid row map: {ex.Message}", null, $"[{n}].definition"));
                continue;
            }
            var problems = await rowMaps.CheckAsync(definition, Pending, ct);
            if (problems.Count > 0)
            {
                result.Issues.AddRange(problems.Take(20).Select(p => Error("ROW_MAP_INVALID", $"Item {n}: {p}", null, $"[{n}].definition")));
                continue;
            }
            var text = definition.ToJson();
            var scope = existing.Where(e => e.Code == request.Code).ToList();
            var current = Current(scope);
            var changes = new List<ContentFieldChangeDto>();
            Diff(changes, "name", current?.Name, request.Name);
            Diff(changes, "rows", current is null ? null : $"{ReportRowMapDefinition.Parse(current.Definition).Rows.Count} rows", $"{definition.Rows.Count} rows");
            Diff(changes, "definition", current is null ? null : ReportRowMapDefinition.Parse(current.Definition).ToJson() == text ? "same" : "changed", "same");
            Diff(changes, "legalBasis", current?.LegalBasis, request.LegalBasis);
            if (Same(scope, s => Equal(s.Name, request.Name) && ReportRowMapDefinition.Parse(s.Definition).ToJson() == text && Equal(s.LegalBasis, request.LegalBasis)))
            {
                unchanged++;
                continue;
            }
            if (Plan(result, scope, n, effective))
            {
                result.Versions.Add(new PlannedVersion(ContentFileKinds.ReportRowMaps, request.Code, $"{request.Code}: {request.Name}", n, request, changes, source));
            }
        }
        return result with { Items = items.Count, Unchanged = unchanged };
    }
}
