using System.Globalization;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Prime.Application.Features.Exemptions;
using Prime.Domain.Enums;

namespace Prime.Application.Features.ContentPacks;

/// <summary>
/// Step L3-1a (docs/analysis/assessment-listing-exemptions.md §4.1, Q13): exemption types as content, keyed by code,
/// imported as Draft versions and approved by a second user like the other catalogues.
/// </summary>
public sealed partial class ContentPackVersionedContent
{
    /// <param name="AppliesTo">Kinds of unit: Land, Building, Machinery, OtherImprovement; omitted: all.</param>
    private sealed record ExemptionTypeItem(string? Code, string? Name, string? Description, List<string>? AppliesTo, bool? RequiresProof,
        decimal? AssessedValueCeiling, string? LegalBasis, string? EffectiveDate, string? Remarks, string? Source);

    private async Task<VersionedPreview> ExemptionTypesAsync(List<ExemptionTypeItem> items, string? fileSource,
        IValidator<CreateExemptionTypeRequest> validator, VersionedPreview result, CancellationToken ct)
    {
        var existing = await db.ExemptionTypes.AsNoTracking().ToListAsync(ct);
        var unchanged = 0;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < items.Count; i++)
        {
            var (n, x) = (i + 1, items[i]);
            if (!Common(result, n, x.Source, fileSource, x.EffectiveDate, out var source, out var effective))
            {
                continue;
            }
            var appliesTo = x.AppliesTo is null or { Count: 0 } ? ExemptionAppliesTo.All : (ExemptionAppliesTo)0;
            var kindsValid = true;
            foreach (var kind in x.AppliesTo ?? [])
            {
                if (!TryEnum<ExemptionAppliesTo>(result, n, "appliesTo", kind, out var flag) || flag == ExemptionAppliesTo.All)
                {
                    kindsValid = false;
                    break;
                }
                appliesTo |= flag;
            }
            if (!kindsValid)
            {
                continue;
            }
            var request = new CreateExemptionTypeRequest(Trim(x.LegalBasis) ?? "", effective, Trim(x.Remarks), Trim(x.Code) ?? "", Trim(x.Name) ?? "",
                Trim(x.Description), appliesTo, x.RequiresProof ?? true, x.AssessedValueCeiling);
            if (!await ValidAsync(validator, request, result, n, ct) || !Unique(result, seen, request.Code, n, "code"))
            {
                continue;
            }
            var scope = existing.Where(e => e.Code == request.Code).ToList();
            var current = Current(scope);
            var changes = new List<ContentFieldChangeDto>();
            Diff(changes, "name", current?.Name, request.Name);
            Diff(changes, "description", current?.Description, request.Description);
            Diff(changes, "appliesTo", current?.AppliesTo.ToString(), request.AppliesTo.ToString());
            Diff(changes, "requiresProof", current is null ? null : current.RequiresProof ? "true" : "false", request.RequiresProof ? "true" : "false");
            Diff(changes, "assessedValueCeiling", current?.AssessedValueCeiling?.ToString(CultureInfo.InvariantCulture),
                request.AssessedValueCeiling?.ToString(CultureInfo.InvariantCulture));
            Diff(changes, "legalBasis", current?.LegalBasis, request.LegalBasis);
            if (Same(scope, s => Equal(s.Name, request.Name) && Equal(s.Description, request.Description) && s.AppliesTo == request.AppliesTo
                    && s.RequiresProof == request.RequiresProof && s.AssessedValueCeiling == request.AssessedValueCeiling && Equal(s.LegalBasis, request.LegalBasis)))
            {
                unchanged++;
                continue;
            }
            if (Plan(result, scope, n, effective))
            {
                result.Versions.Add(new PlannedVersion(ContentFileKinds.ExemptionTypes, request.Code, request.Name, n, request, changes, source));
            }
        }
        return result with { Items = items.Count, Unchanged = unchanged };
    }
}
