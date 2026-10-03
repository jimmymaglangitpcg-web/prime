using System.Globalization;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Prime.Application.Common;
using Prime.Application.Features.AssessmentLevels;

namespace Prime.Application.Features.ContentPacks;

/// <summary>A ceiling whose lookups are resolved by code at import (the pack may add them).</summary>
public sealed record PackLevelCeiling(string PropertyType, string? Classification, string? ActualUse, CreateAssessmentLevelCeilingRequest Values);

/// <summary>
/// Step L3-2 (docs/analysis/assessment-listing-exemptions.md §4.2, Q6): statutory maximum assessment levels as content,
/// keyed by code, imported as Draft versions and approved by a second user.
/// </summary>
public sealed partial class ContentPackVersionedContent
{
    private sealed record LevelCeilingItem(string? Code, string? Description, string? PropertyType, string? Classification, string? ActualUse,
        decimal? LowerValue, decimal? UpperValue, decimal? MaximumPercentage, string? LegalBasis, string? EffectiveDate, string? Remarks, string? Source);

    private async Task<VersionedPreview> LevelCeilingsAsync(List<LevelCeilingItem> items, string? fileSource, PackPending pending,
        IValidator<CreateAssessmentLevelCeilingRequest> validator, VersionedPreview result, CancellationToken ct)
    {
        var codes = await CodesAsync(ct);
        var existing = await db.AssessmentLevelCeilings.AsNoTracking().ToListAsync(ct);
        var unchanged = 0;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < items.Count; i++)
        {
            var (n, x) = (i + 1, items[i]);
            var newCode = false;
            if (!Common(result, n, x.Source, fileSource, x.EffectiveDate, out var source, out var effective)
                || !PackCode(result, n, "propertyType", "property-types", Trim(x.PropertyType), true, codes, pending, out var propertyType, ref newCode)
                || !PackCode(result, n, "classification", "classifications", Trim(x.Classification), false, codes, pending, out var classification, ref newCode)
                || !PackCode(result, n, "actualUse", "actual-uses", Trim(x.ActualUse), false, codes, pending, out var actualUse, ref newCode))
            {
                continue;
            }
            // A code the pack adds has no id yet: a placeholder for validation, resolved at import.
            var request = new CreateAssessmentLevelCeilingRequest(Trim(x.LegalBasis) ?? "", effective, Trim(x.Remarks), Trim(x.Code) ?? "",
                Trim(x.Description), propertyType ?? Guid.NewGuid(), classification ?? (Trim(x.Classification) is null ? null : Guid.NewGuid()),
                actualUse ?? (Trim(x.ActualUse) is null ? null : Guid.NewGuid()), x.LowerValue ?? 0m, x.UpperValue, x.MaximumPercentage ?? 0m);
            if (!await ValidAsync(validator, request, result, n, ct) || !Unique(result, seen, request.Code, n, "code"))
            {
                continue;
            }
            var scope = existing.Where(e => e.Code == request.Code).ToList();
            var current = Current(scope);
            var changes = new List<ContentFieldChangeDto>();
            static string? Num(decimal? v) => v?.ToString(CultureInfo.InvariantCulture);
            Diff(changes, "maximumPercentage", Num(current?.MaximumPercentage), Num(request.MaximumPercentage));
            Diff(changes, "bracket", current is null ? null : $"{Num(current.LowerValue)} – {Num(current.UpperValue) ?? "up"}",
                $"{Num(request.LowerValue)} – {Num(request.UpperValue) ?? "up"}");
            string? CodeOf(string lookup, Guid? id) => id is { } v ? codes[lookup].FirstOrDefault(kv => kv.Value == v).Key : null;
            Diff(changes, "keys", current is null ? null
                    : $"{CodeOf("property-types", current.PropertyTypeId)} / {CodeOf("classifications", current.ClassificationId) ?? "any"} / {CodeOf("actual-uses", current.ActualUseId) ?? "any"}",
                $"{Trim(x.PropertyType)} / {Trim(x.Classification) ?? "any"} / {Trim(x.ActualUse) ?? "any"}");
            Diff(changes, "description", current?.Description, request.Description);
            Diff(changes, "legalBasis", current?.LegalBasis, request.LegalBasis);
            if (!newCode && Same(scope, s => s.PropertyTypeId == request.PropertyTypeId && s.ClassificationId == request.ClassificationId
                    && s.ActualUseId == request.ActualUseId && s.LowerValue == request.LowerValue && s.UpperValue == request.UpperValue
                    && s.MaximumPercentage == request.MaximumPercentage && Equal(s.Description, request.Description) && Equal(s.LegalBasis, request.LegalBasis)))
            {
                unchanged++;
                continue;
            }
            if (Plan(result, scope, n, effective))
            {
                result.Versions.Add(new PlannedVersion(ContentFileKinds.AssessmentLevelCeilings, request.Code,
                    $"{request.Code}: max {Num(request.MaximumPercentage)}%", n,
                    new PackLevelCeiling(Trim(x.PropertyType)!, Trim(x.Classification), Trim(x.ActualUse), request), changes, source));
            }
        }
        return result with { Items = items.Count, Unchanged = unchanged };
    }

    private async Task<Result<(string, Guid)>> CreateLevelCeilingAsync(PackLevelCeiling r, IAssessmentLevelCeilingService ceilings, CancellationToken ct)
    {
        var c = await CodesAsync(ct);
        if (!c["property-types"].TryGetValue(r.PropertyType, out var propertyType)
            || (r.Classification is { } cl && !c["classifications"].ContainsKey(cl))
            || (r.ActualUse is { } au && !c["actual-uses"].ContainsKey(au)))
        {
            return Result.Failure<(string, Guid)>("CONTENT_PACK_IMPORT_FAILED", $"A code of ceiling {r.Values.Code} was not found at import.");
        }
        var values = r.Values with
        {
            PropertyTypeId = propertyType,
            ClassificationId = r.Classification is null ? null : c["classifications"][r.Classification],
            ActualUseId = r.ActualUse is null ? null : c["actual-uses"][r.ActualUse],
        };
        return Map("AssessmentLevelCeiling", await ceilings.CreateAsync(values, ct), x => x.Id);
    }
}
