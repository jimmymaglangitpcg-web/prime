using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Prime.Application.Features.GeneralRevision;
using Prime.Domain.Enums;

namespace Prime.Application.Features.ContentPacks;

/// <summary>
/// Step L6-6c (docs/analysis/smv-preparation-general-revision.md §4.6, Q13): the general revision instructions' checklist as
/// content, keyed by step code, imported as Draft versions and approved by a second user. The LAM's text stays out of the
/// repository (CLAUDE.md §118); a step may name a gate PRIME checks itself.
/// </summary>
public sealed partial class ContentPackVersionedContent
{
    private sealed record ChecklistStepItem(string? Code, int? Sequence, string? Title, string? Description, string? Gate, string? LegalBasis,
        string? EffectiveDate, string? Remarks, string? Source);

    private async Task<VersionedPreview> ChecklistAsync(List<ChecklistStepItem> items, string? fileSource,
        IValidator<CreateChecklistStepDefinitionRequest> validator, VersionedPreview result, CancellationToken ct)
    {
        var existing = await db.GeneralRevisionChecklistStepDefinitions.AsNoTracking().ToListAsync(ct);
        var unchanged = 0;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < items.Count; i++)
        {
            var (n, x) = (i + 1, items[i]);
            if (!Common(result, n, x.Source, fileSource, x.EffectiveDate, out var source, out var effective))
            {
                continue;
            }
            GeneralRevisionGate? gate = null;
            if (!string.IsNullOrWhiteSpace(x.Gate))
            {
                if (!TryEnum<GeneralRevisionGate>(result, n, "gate", x.Gate, out var g))
                {
                    continue;
                }
                gate = g;
            }
            var request = new CreateChecklistStepDefinitionRequest(Trim(x.LegalBasis) ?? "", effective, Trim(x.Remarks), Trim(x.Code) ?? "", x.Sequence ?? 0,
                Trim(x.Title) ?? "", Trim(x.Description), gate);
            if (!await ValidAsync(validator, request, result, n, ct) || !Unique(result, seen, request.Code, n, "code"))
            {
                continue;
            }
            var scope = existing.Where(e => e.Code == request.Code).ToList();
            var current = Current(scope);
            var changes = new List<ContentFieldChangeDto>();
            Diff(changes, "sequence", current?.Sequence.ToString(), request.Sequence.ToString());
            Diff(changes, "title", current?.Title, request.Title);
            Diff(changes, "description", current?.Description, request.Description);
            Diff(changes, "gate", current?.Gate?.ToString(), request.Gate?.ToString());
            Diff(changes, "legalBasis", current?.LegalBasis, request.LegalBasis);
            if (Same(scope, s => s.Sequence == request.Sequence && Equal(s.Title, request.Title) && Equal(s.Description, request.Description)
                    && s.Gate == request.Gate && Equal(s.LegalBasis, request.LegalBasis)))
            {
                unchanged++;
                continue;
            }
            if (Plan(result, scope, n, effective))
            {
                result.Versions.Add(new PlannedVersion(ContentFileKinds.GeneralRevisionChecklist, request.Code, request.Title, n, request, changes, source));
            }
        }
        return result with { Items = items.Count, Unchanged = unchanged };
    }
}
