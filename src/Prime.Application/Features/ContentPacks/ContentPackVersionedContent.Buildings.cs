using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Prime.Application.Common;
using Prime.Application.Features.Smv;
using Prime.Domain.DomainServices;
using Prime.Domain.Enums;

namespace Prime.Application.Features.ContentPacks;

/// <summary>A construction cost whose SMV and codes are resolved at import (step L1-5).</summary>
public sealed record PackBuildingCost(string SmvReference, string StructuralType, string? BuildingType, string? Classification, CreateBuildingCostRequest Values);

/// <summary>An extra-item cost whose SMV and component type are resolved at import (step L1-5).</summary>
public sealed record PackExtraItemCost(string SmvReference, string ComponentType, CreateExtraItemCostRequest Values);

/// <summary>A depreciation table whose SMV and structural type are resolved at import (step L1-5).</summary>
public sealed record PackDepreciationSchedule(string SmvReference, string StructuralType, CreateDepreciationScheduleRequest Values);

/// <summary>
/// Step L1-5 (docs/analysis/valuation-foundation.md §4.5): the SMV's building tables — construction
/// costs, extra-item costs and depreciation tables — as JSON catalogues, imported as <b>Draft</b>
/// versions for a second user to approve, keyed by SMV and the table's scope.
/// </summary>
public sealed partial class ContentPackVersionedContent
{
    private sealed record BuildingCostItem(string? Smv, string? StructuralType, string? BuildingType, string? Classification, decimal? CostPerSquareMetre,
        string? LegalBasis, string? EffectiveDate, string? Remarks, string? Source);

    private sealed record ExtraItemCostItem(string? Smv, string? ComponentType, string? Unit, decimal? UnitCost,
        string? LegalBasis, string? EffectiveDate, string? Remarks, string? Source);

    private sealed record DepreciationItem(string? Smv, string? StructuralType, string? Reading, decimal? MinimumRemainingPercent, List<DepreciationRowItem>? Rows,
        string? LegalBasis, string? EffectiveDate, string? Remarks, string? Source);

    private sealed record DepreciationRowItem(int? FromAge, int? ToAge, decimal? Percent);

    // Decimals compared as written, since the database pads them (1 vs 1.000).
    private static string D(decimal? v) => v?.ToString("0.######", CultureInfo.InvariantCulture) ?? "";

    // --- Construction costs (key: SMV + structural type + kind + classification) ---

    private async Task<VersionedPreview> BuildingCostsAsync(List<BuildingCostItem> items, string? fileSource, PackPending pending, VersionedPreview result,
        CancellationToken ct)
    {
        var smvIds = await SmvIdsAsync(ct);
        var codes = await BuildingCodesAsync(ct);
        var existing = await db.SmvBuildingCosts.AsNoTracking().ToListAsync(ct);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var unchanged = 0;
        for (var i = 0; i < items.Count; i++)
        {
            var (n, x) = (i + 1, items[i]);
            var smvRef = Trim(x.Smv) ?? "";
            var newCode = false;
            if (!Common(result, n, x.Source, fileSource, x.EffectiveDate, out var source, out var effective)
                || !KnownSmv(result, n, smvRef, smvIds, pending)
                || !PackCode(result, n, "structuralType", "structural-types", Trim(x.StructuralType), true, codes, pending, out var structure, ref newCode)
                || !PackCode(result, n, "buildingType", "building-types", Trim(x.BuildingType), false, codes, pending, out var kind, ref newCode)
                || !PackCode(result, n, "classification", "classifications", Trim(x.Classification), false, codes, pending, out var classification, ref newCode))
            {
                continue;
            }
            var values = new CreateBuildingCostRequest(smvIds.GetValueOrDefault(smvRef), structure ?? Guid.Empty, kind, classification,
                x.CostPerSquareMetre ?? 0m, Trim(x.LegalBasis) ?? source, effective, Trim(x.Remarks));
            if (values.CostPerSquareMetre <= 0m || values.LegalBasis.Length > 500)
            {
                result.Issues.Add(Error("VALIDATION_FAILED", $"Item {n}: costPerSquareMetre above 0 is required; legalBasis max 500.", null, $"[{n}]"));
                continue;
            }
            var key = string.Join(" ", new[] { smvRef, Trim(x.StructuralType), Trim(x.BuildingType), Trim(x.Classification) }.Where(s => s is not null));
            if (!Unique(result, seen, key, n, "structuralType/buildingType/classification"))
            {
                continue;
            }
            var scope = smvIds.TryGetValue(smvRef, out var smvId) && !newCode
                ? existing.Where(c => c.SmvId == smvId && c.StructuralTypeId == structure && c.BuildingTypeId == kind && c.ClassificationId == classification).ToList()
                : [];
            if (Same(scope, c => c.CostPerSquareMetre == values.CostPerSquareMetre && Equal(c.LegalBasis, values.LegalBasis)))
            {
                unchanged++;
                continue;
            }
            if (Plan(result, scope, n, effective))
            {
                var changes = new List<ContentFieldChangeDto>();
                Diff(changes, "costPerSquareMetre", Current(scope) is { } current ? D(current.CostPerSquareMetre) : null, D(values.CostPerSquareMetre));
                result.Versions.Add(new PlannedVersion(ContentFileKinds.BuildingCosts, key, $"{key}: {D(values.CostPerSquareMetre)}/sqm", n,
                    new PackBuildingCost(smvRef, Trim(x.StructuralType)!, Trim(x.BuildingType), Trim(x.Classification), values), changes, source));
            }
        }
        return result with { Items = items.Count, Unchanged = unchanged };
    }

    // --- Extra-item costs (key: SMV + component type) ---

    private async Task<VersionedPreview> ExtraItemCostsAsync(List<ExtraItemCostItem> items, string? fileSource, PackPending pending, VersionedPreview result,
        CancellationToken ct)
    {
        var smvIds = await SmvIdsAsync(ct);
        var codes = await BuildingCodesAsync(ct);
        var existing = await db.SmvExtraItemCosts.AsNoTracking().ToListAsync(ct);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var unchanged = 0;
        for (var i = 0; i < items.Count; i++)
        {
            var (n, x) = (i + 1, items[i]);
            var smvRef = Trim(x.Smv) ?? "";
            var newCode = false;
            if (!Common(result, n, x.Source, fileSource, x.EffectiveDate, out var source, out var effective)
                || !KnownSmv(result, n, smvRef, smvIds, pending)
                || !PackCode(result, n, "componentType", "building-component-types", Trim(x.ComponentType), true, codes, pending, out var component, ref newCode))
            {
                continue;
            }
            var values = new CreateExtraItemCostRequest(smvIds.GetValueOrDefault(smvRef), component ?? Guid.Empty, Trim(x.Unit) ?? "", x.UnitCost ?? 0m,
                Trim(x.LegalBasis) ?? source, effective, Trim(x.Remarks));
            if (values.UnitCost <= 0m || values.Unit.Length is 0 or > 30 || values.LegalBasis.Length > 500)
            {
                result.Issues.Add(Error("VALIDATION_FAILED", $"Item {n}: unit (max 30) and a unitCost above 0 are required; legalBasis max 500.", null, $"[{n}]"));
                continue;
            }
            var key = $"{smvRef} {Trim(x.ComponentType)}";
            if (!Unique(result, seen, key, n, "componentType"))
            {
                continue;
            }
            var scope = smvIds.TryGetValue(smvRef, out var smvId) && !newCode
                ? existing.Where(c => c.SmvId == smvId && c.ComponentTypeId == component).ToList()
                : [];
            if (Same(scope, c => c.UnitCost == values.UnitCost && Equal(c.Unit, values.Unit) && Equal(c.LegalBasis, values.LegalBasis)))
            {
                unchanged++;
                continue;
            }
            if (Plan(result, scope, n, effective))
            {
                var current = Current(scope);
                var changes = new List<ContentFieldChangeDto>();
                Diff(changes, "unitCost", current is null ? null : D(current.UnitCost), D(values.UnitCost));
                Diff(changes, "unit", current?.Unit, values.Unit);
                result.Versions.Add(new PlannedVersion(ContentFileKinds.ExtraItemCosts, key, $"{key}: {D(values.UnitCost)} per {values.Unit}", n,
                    new PackExtraItemCost(smvRef, Trim(x.ComponentType)!, values), changes, source));
            }
        }
        return result with { Items = items.Count, Unchanged = unchanged };
    }

    // --- Depreciation tables (key: SMV + structural type) ---

    private async Task<VersionedPreview> DepreciationAsync(List<DepreciationItem> items, string? fileSource, PackPending pending, VersionedPreview result,
        CancellationToken ct)
    {
        var smvIds = await SmvIdsAsync(ct);
        var codes = await BuildingCodesAsync(ct);
        var existing = await db.SmvDepreciationSchedules.AsNoTracking().Include(x => x.Rows).ToListAsync(ct);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var unchanged = 0;
        for (var i = 0; i < items.Count; i++)
        {
            var (n, x) = (i + 1, items[i]);
            var smvRef = Trim(x.Smv) ?? "";
            var newCode = false;
            if (!Common(result, n, x.Source, fileSource, x.EffectiveDate, out var source, out var effective)
                || !TryEnum<DepreciationReading>(result, n, "reading", x.Reading ?? "", out var reading)
                || !KnownSmv(result, n, smvRef, smvIds, pending)
                || !PackCode(result, n, "structuralType", "structural-types", Trim(x.StructuralType), true, codes, pending, out var structure, ref newCode))
            {
                continue;
            }
            var rows = (x.Rows ?? []).Select(r => new DepreciationRowRequest(r.FromAge ?? -1, r.ToAge, r.Percent ?? -1m)).ToList();
            var remaining = x.MinimumRemainingPercent ?? -1m;
            var problem = remaining is < 0m or > 100m ? "minimumRemainingPercent from 0 to 100 is required."
                : BuildingDepreciation.BandsProblem(rows.Select(r => (r.FromAge, r.ToAge, r.Percent)).ToList());
            if (problem is not null)
            {
                result.Issues.Add(Error("VALIDATION_FAILED", $"Item {n}: {problem}", null, $"[{n}]"));
                continue;
            }
            var values = new CreateDepreciationScheduleRequest(smvIds.GetValueOrDefault(smvRef), structure ?? Guid.Empty, reading, remaining,
                rows.OrderBy(r => r.FromAge).ToList(), Trim(x.LegalBasis) ?? source, effective, Trim(x.Remarks));
            var key = $"{smvRef} {Trim(x.StructuralType)}";
            if (!Unique(result, seen, key, n, "structuralType"))
            {
                continue;
            }
            var rowText = string.Join(" | ", values.Rows.Select(r => $"{r.FromAge}-{r.ToAge}={D(r.Percent)}"));
            static string Rows(Domain.Entities.SmvDepreciationSchedule s) =>
                string.Join(" | ", s.Rows.OrderBy(r => r.FromAge).Select(r => $"{r.FromAge}-{r.ToAge}={D(r.Percent)}"));
            var scope = smvIds.TryGetValue(smvRef, out var smvId) && !newCode
                ? existing.Where(s => s.SmvId == smvId && s.StructuralTypeId == structure).ToList()
                : [];
            if (Same(scope, s => s.Reading == reading && s.MinimumRemainingPercent == remaining && Equal(s.LegalBasis, values.LegalBasis) && Rows(s) == rowText))
            {
                unchanged++;
                continue;
            }
            if (Plan(result, scope, n, effective))
            {
                var current = Current(scope);
                var changes = new List<ContentFieldChangeDto>();
                Diff(changes, "reading", current?.Reading.ToString(), reading.ToString());
                Diff(changes, "minimumRemainingPercent", current is null ? null : D(current.MinimumRemainingPercent), D(remaining));
                Diff(changes, "rows", current is null ? null : Rows(current), rowText);
                result.Versions.Add(new PlannedVersion(ContentFileKinds.DepreciationRates, key, $"{key}: {values.Rows.Count} age bands ({reading})", n,
                    new PackDepreciationSchedule(smvRef, Trim(x.StructuralType)!, values), changes, source));
            }
        }
        return result with { Items = items.Count, Unchanged = unchanged };
    }

    // --- Import ---

    private async Task<Result<(string, Guid)>> CreateBuildingCostAsync(PackBuildingCost r, CancellationToken ct)
    {
        if (await SmvIdAsync(r.SmvReference, ct) is not { } smvId)
        {
            return Result.Failure<(string, Guid)>("CONTENT_PACK_IMPORT_FAILED", $"SMV {r.SmvReference} was not found at import.");
        }
        var c = await BuildingCodesAsync(ct);
        var values = r.Values with
        {
            SmvId = smvId,
            StructuralTypeId = c["structural-types"][r.StructuralType],
            BuildingTypeId = r.BuildingType is null ? null : c["building-types"][r.BuildingType],
            ClassificationId = r.Classification is null ? null : c["classifications"][r.Classification],
        };
        return Map("SmvBuildingCost", await buildingTables.CreateBuildingCostAsync(values, ct), x => x.Id);
    }

    private async Task<Result<(string, Guid)>> CreateExtraItemCostAsync(PackExtraItemCost r, CancellationToken ct)
    {
        if (await SmvIdAsync(r.SmvReference, ct) is not { } smvId)
        {
            return Result.Failure<(string, Guid)>("CONTENT_PACK_IMPORT_FAILED", $"SMV {r.SmvReference} was not found at import.");
        }
        var c = await BuildingCodesAsync(ct);
        var values = r.Values with { SmvId = smvId, ComponentTypeId = c["building-component-types"][r.ComponentType] };
        return Map("SmvExtraItemCost", await buildingTables.CreateExtraItemCostAsync(values, ct), x => x.Id);
    }

    private async Task<Result<(string, Guid)>> CreateDepreciationScheduleAsync(PackDepreciationSchedule r, CancellationToken ct)
    {
        if (await SmvIdAsync(r.SmvReference, ct) is not { } smvId)
        {
            return Result.Failure<(string, Guid)>("CONTENT_PACK_IMPORT_FAILED", $"SMV {r.SmvReference} was not found at import.");
        }
        var c = await BuildingCodesAsync(ct);
        var values = r.Values with { SmvId = smvId, StructuralTypeId = c["structural-types"][r.StructuralType] };
        return Map("SmvDepreciationSchedule", await buildingTables.CreateDepreciationScheduleAsync(values, ct), x => x.Id);
    }

    // --- Helpers ---

    private async Task<Dictionary<string, Guid>> SmvIdsAsync(CancellationToken ct) =>
        (await db.Smvs.AsNoTracking().Select(x => new { x.Id, x.OrdinanceNumber, x.CertificationReference }).ToListAsync(ct))
            .ToDictionary(x => x.OrdinanceNumber ?? x.CertificationReference ?? "", x => x.Id, StringComparer.Ordinal);

    private async Task<Guid?> SmvIdAsync(string reference, CancellationToken ct) =>
        await db.Smvs.Where(x => x.OrdinanceNumber == reference || x.CertificationReference == reference).Select(x => (Guid?)x.Id).FirstOrDefaultAsync(ct);

    private async Task<Dictionary<string, Dictionary<string, Guid>>> BuildingCodesAsync(CancellationToken ct) => new()
    {
        ["structural-types"] = await db.StructuralTypes.AsNoTracking().ToDictionaryAsync(x => x.Code, x => x.Id, StringComparer.Ordinal, ct),
        ["building-types"] = await db.BuildingTypes.AsNoTracking().ToDictionaryAsync(x => x.Code, x => x.Id, StringComparer.Ordinal, ct),
        ["building-component-types"] = await db.BuildingComponentTypes.AsNoTracking().ToDictionaryAsync(x => x.Code, x => x.Id, StringComparer.Ordinal, ct),
        ["classifications"] = await db.Classifications.AsNoTracking().ToDictionaryAsync(x => x.Code, x => x.Id, StringComparer.Ordinal, ct),
    };

    private static bool KnownSmv(VersionedPreview result, int n, string smvRef, Dictionary<string, Guid> smvIds, PackPending pending)
    {
        if (smvIds.ContainsKey(smvRef) || pending.Smvs.Contains(smvRef))
        {
            return true;
        }
        result.Issues.Add(Error("SMV_UNKNOWN", $"Item {n}: SMV {smvRef} is neither in PRIME nor added by an earlier smv file of this pack.", null, $"[{n}].smv"));
        return false;
    }

    /// <summary>Resolves a code to an id; a code only the pack adds resolves to null and marks the item as new (it cannot match a record yet).</summary>
    private static bool PackCode(VersionedPreview result, int n, string field, string lookup, string? code, bool required,
        Dictionary<string, Dictionary<string, Guid>> codes, PackPending pending, out Guid? id, ref bool pendingCode)
    {
        id = null;
        if (code is null)
        {
            if (required)
            {
                result.Issues.Add(Error("VALUE_MISSING", $"Item {n}: {field} is required.", null, $"[{n}].{field}"));
            }
            return !required;
        }
        if (codes[lookup].TryGetValue(code, out var found))
        {
            id = found;
            return true;
        }
        if (pending.Lookups.TryGetValue(lookup, out var added) && added.Contains(code))
        {
            pendingCode = true;
            return true;
        }
        result.Issues.Add(Error("CODE_UNKNOWN", $"Item {n}: {field} {code} is neither in PRIME nor added by this pack ({lookup}).", null, $"[{n}].{field}"));
        return false;
    }
}
