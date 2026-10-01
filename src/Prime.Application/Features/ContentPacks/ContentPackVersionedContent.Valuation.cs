using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Prime.Application.Common;
using Prime.Application.Features.AssessmentLevels;
using Prime.Application.Features.Smv;
using Prime.Domain.Enums;

namespace Prime.Application.Features.ContentPacks;

/// <summary>
/// What a pack adds that later files of the same pack may refer to: new municipalities and
/// barangays (PSGC codes), new lookup codes by lookup name, and SMVs by reference.
/// </summary>
public sealed record PackPending(
    IReadOnlySet<string> Barangays,
    IReadOnlyDictionary<string, IReadOnlySet<string>> Lookups,
    IReadOnlySet<string> Smvs)
{
    public static readonly PackPending None = new(new HashSet<string>(), new Dictionary<string, IReadOnlySet<string>>(), new HashSet<string>());
}

/// <summary>An SMV whose coverage is resolved by PSGC code at import, when the pack's municipalities exist (step L1-3).</summary>
public sealed record PackSmv(IReadOnlyList<string> MunicipalityPsgcCodes, CreateSmvRequest Request);

/// <summary>A unit value whose SMV and codes are resolved at import, when the pack's SMVs and lookups exist (step L1-3).</summary>
public sealed record PackSmvSchedule(string SmvReference, string Classification, string? SubClassification, string? ActualUse, string PropertyType,
    string? Zone, string? BarangayPsgc, string? ImprovementKind, CreateSmvScheduleRequest Values);

/// <summary>An assessment level whose codes are resolved at import (step L1-3).</summary>
public sealed record PackAssessmentLevel(string Classification, string ActualUse, string PropertyType, CreateAssessmentLevelRequest Values);

/// <summary>
/// Step L1-3 (docs/analysis/valuation-foundation.md §4.3): the SMV, its land and improvement
/// unit values, and assessment levels. Like the other legal configuration they become
/// <b>Draft</b> records for a second user to approve. An SMV is never edited: an SMV with the
/// same reference and the same content is unchanged; with different content it is refused.
/// </summary>
public sealed partial class ContentPackVersionedContent
{
    private sealed record SmvItem(string? Basis, string? OrdinanceNumber, string? OrdinanceDate, string? ApprovalDate, string? CertificationReference,
        string? CertifiedOn, string? ProposedOn, string? PublishedForCommentOn, string? ConsultationsHeldOn, string? SubmittedToBlgfOn, string? PublishedOn,
        string? PublicationReference, string? EffectivityDate, int? RevisionYear, string? Description, List<string>? Municipalities, string? Source);

    // --- SMVs (key: the ordinance number or certification reference) ---

    private async Task<VersionedPreview> SmvsAsync(List<SmvItem> items, string? fileSource, IReadOnlySet<string> pendingMunicipalities, VersionedPreview result,
        CancellationToken ct)
    {
        var existing = await db.Smvs.AsNoTracking().Include(x => x.Coverage).ThenInclude(c => c.Municipality).ToListAsync(ct);
        var municipalities = await db.Municipalities.AsNoTracking().ToDictionaryAsync(x => x.PsgcCode, x => x.Id, StringComparer.Ordinal, ct);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var unchanged = 0;
        for (var i = 0; i < items.Count; i++)
        {
            var (n, x) = (i + 1, items[i]);
            if (!Common(result, n, x.Source, fileSource, x.EffectivityDate, out var source, out var effective)
                || !TryEnum<SmvBasis>(result, n, "basis", x.Basis ?? nameof(SmvBasis.Ordinance), out var basis)
                || !Dates(result, n, x, out var dates))
            {
                continue;
            }
            var codes = (x.Municipalities ?? []).Select(c => c.Trim()).ToList();
            var unknown = codes.Where(c => !municipalities.ContainsKey(c) && !pendingMunicipalities.Contains(c)).ToList();
            if (unknown.Count > 0)
            {
                result.Issues.Add(Error("MUNICIPALITY_UNKNOWN", $"Item {n}: municipality {string.Join(", ", unknown)} is neither in PRIME nor added by this pack.",
                    null, $"[{n}].municipalities"));
                continue;
            }
            var request = new CreateSmvRequest(Trim(x.OrdinanceNumber), dates["ordinanceDate"], dates["approvalDate"], effective, x.RevisionYear ?? 0,
                Trim(x.Description), basis, dates["proposedOn"], dates["publishedForCommentOn"], dates["consultationsHeldOn"], dates["submittedToBlgfOn"],
                dates["certifiedOn"], Trim(x.CertificationReference), dates["publishedOn"], Trim(x.PublicationReference),
                codes.Where(municipalities.ContainsKey).Select(c => municipalities[c]).ToList());
            if (!await ValidAsync(smvValidator, request, result, n, ct))
            {
                continue;
            }
            var reference = basis == SmvBasis.Certified ? request.CertificationReference! : request.OrdinanceNumber!;
            if (!Unique(result, seen, reference, n, "reference"))
            {
                continue;
            }
            var coverage = string.Join(",", codes.Order(StringComparer.Ordinal));
            if (existing.FirstOrDefault(s => s.Reference == reference) is { } current)
            {
                var same = current.Basis == basis && current.EffectivityDate == effective && current.RevisionYear == request.RevisionYear
                    && string.Join(",", current.Coverage.Select(c => c.Municipality!.PsgcCode).Order(StringComparer.Ordinal)) == coverage;
                if (same)
                {
                    unchanged++;
                }
                else
                {
                    result.Issues.Add(Error("SMV_EXISTS", $"Item {n}: SMV {reference} is already in PRIME with other content. An SMV is never edited; a new one needs its own reference.",
                        null, $"[{n}]"));
                }
                continue;
            }
            result.Versions.Add(new PlannedVersion(ContentFileKinds.Smv, reference, $"SMV {reference} ({request.RevisionYear})", n, new PackSmv(codes, request),
                [
                    new ContentFieldChangeDto("basis", null, basis.ToString()),
                    new ContentFieldChangeDto("effectivityDate", null, effective.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
                    new ContentFieldChangeDto("coverage", null, codes.Count == 0 ? "whole province" : coverage),
                ], source));
        }
        return result with { Items = items.Count, Unchanged = unchanged };
    }

    private static bool Dates(VersionedPreview result, int n, SmvItem x, out Dictionary<string, DateOnly?> dates)
    {
        dates = new Dictionary<string, DateOnly?>();
        var ok = true;
        foreach (var (field, text) in new[]
        {
            ("ordinanceDate", x.OrdinanceDate), ("approvalDate", x.ApprovalDate), ("certifiedOn", x.CertifiedOn), ("proposedOn", x.ProposedOn),
            ("publishedForCommentOn", x.PublishedForCommentOn), ("consultationsHeldOn", x.ConsultationsHeldOn), ("submittedToBlgfOn", x.SubmittedToBlgfOn),
            ("publishedOn", x.PublishedOn),
        })
        {
            if (Trim(text) is null)
            {
                dates[field] = null;
            }
            else if (DateOnly.TryParseExact(Trim(text), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
            {
                dates[field] = d;
            }
            else
            {
                result.Issues.Add(Error("DATE_INVALID", $"Item {n}: {field} must be a date written yyyy-MM-dd; got '{text}'.", null, $"[{n}].{field}"));
                ok = false;
            }
        }
        return ok;
    }

    // --- Unit values (CSV; key: SMV + the rate key + effective date) ---

    private static readonly string[] ScheduleColumns =
        ["smv", "classification", "sub-classification", "actual-use", "property-type", "zone", "barangay", "improvement-kind", "unit", "market-value",
         "minimum-value", "maximum-value", "effective-date", "source"];

    private async Task<VersionedPreview> SchedulesAsync(byte[] bytes, string? fileSource, PackPending pending, VersionedPreview result, CancellationToken ct)
    {
        if (Table(bytes, ScheduleColumns, ["smv", "classification", "property-type", "market-value", "effective-date"], result) is not { } table)
        {
            return result;
        }
        var smvs = (await db.Smvs.AsNoTracking().Select(x => new { x.Id, x.OrdinanceNumber, x.CertificationReference }).ToListAsync(ct))
            .ToDictionary(x => x.OrdinanceNumber ?? x.CertificationReference ?? "", x => x.Id, StringComparer.Ordinal);
        var codes = await CodesAsync(ct);
        var barangays = await db.Barangays.AsNoTracking().ToDictionaryAsync(x => x.PsgcCode, x => x.Id, StringComparer.Ordinal, ct);
        var existing = await db.SmvSchedules.AsNoTracking()
            .Where(x => x.Status == WorkflowStatus.Draft || x.Status == WorkflowStatus.Approved).ToListAsync(ct);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var unchanged = 0;
        foreach (var row in table.Rows)
        {
            var line = row.Line;
            var smvRef = row.Get("smv")!;
            if (!smvs.ContainsKey(smvRef) && !pending.Smvs.Contains(smvRef))
            {
                result.Issues.Add(Error("SMV_UNKNOWN", $"Line {line}: SMV {smvRef} is neither in PRIME nor added by an earlier smv file of this pack.", line, "smv"));
                continue;
            }
            var ids = new Dictionary<string, Guid?>();
            var ok = true;
            foreach (var (column, lookup, required) in new[]
            {
                ("classification", "classifications", true), ("sub-classification", "sub-classifications", false), ("actual-use", "actual-uses", false),
                ("property-type", "property-types", true), ("zone", "zones", false), ("improvement-kind", "improvement-kinds", false),
            })
            {
                ok &= Code(result, row, column, lookup, required, codes, pending, ids);
            }
            var barangay = row.Get("barangay");
            if (barangay is not null && !barangays.ContainsKey(barangay) && !pending.Barangays.Contains(barangay))
            {
                result.Issues.Add(Error("BARANGAY_UNKNOWN", $"Line {line}: barangay {barangay} is neither in PRIME nor added by this pack.", line, "barangay"));
                ok = false;
            }
            if (!ok || !Money(result, row, "market-value", out var value) || !OptionalMoney(result, row, "minimum-value", out var min)
                || !OptionalMoney(result, row, "maximum-value", out var max) || !RowDate(result, row, "effective-date", out var effective))
            {
                continue;
            }
            var values = new CreateSmvScheduleRequest(ids["classification"] ?? Guid.NewGuid(), ids["actual-use"], ids["property-type"] ?? Guid.NewGuid(),
                ids["zone"], row.Get("unit") ?? "per sqm", value, min, max, effective, ids["improvement-kind"], ids["sub-classification"],
                barangay is not null && barangays.TryGetValue(barangay, out var bId) ? bId : null);
            if (!await ValidAsync(scheduleValidator, values, result, line, ct))
            {
                continue;
            }
            var key = string.Join("|", smvRef, row.Get("classification"), row.Get("sub-classification"), row.Get("actual-use"), row.Get("property-type"),
                row.Get("zone"), barangay, row.Get("improvement-kind"), effective.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            if (!Unique(result, seen, key, line, "rate"))
            {
                continue;
            }
            // Unchanged: the same SMV already holds this rate with this value (approved or awaiting approval).
            var allKnown = smvs.TryGetValue(smvRef, out var smvId) && !PendingCode(row, pending) && (barangay is null || barangays.ContainsKey(barangay));
            if (allKnown && existing.Any(s => s.SmvId == smvId && s.ClassificationId == values.ClassificationId && s.SubClassificationId == values.SubClassificationId
                    && s.ActualUseId == values.ActualUseId && s.PropertyTypeId == values.PropertyTypeId && s.ZoneId == values.ZoneId && s.BarangayId == values.BarangayId
                    && s.ImprovementKindId == values.ImprovementKindId && s.EffectiveDate == effective && s.MarketValue == value && s.Unit == values.Unit
                    && s.MinimumValue == min && s.MaximumValue == max))
            {
                unchanged++;
                continue;
            }
            var source = row.Get("source") ?? fileSource ?? "";
            result.Versions.Add(new PlannedVersion(ContentFileKinds.SmvSchedules, key, $"{smvRef}: {row.Get("classification")} {row.Get("sub-classification")}".Trim(), line,
                new PackSmvSchedule(smvRef, row.Get("classification")!, row.Get("sub-classification"), row.Get("actual-use"), row.Get("property-type")!,
                    row.Get("zone"), barangay, row.Get("improvement-kind"), values),
                [new ContentFieldChangeDto("marketValue", null, value.ToString(CultureInfo.InvariantCulture)), new ContentFieldChangeDto("unit", null, values.Unit)],
                source));
        }
        return result with { Items = table.Rows.Count, Unchanged = unchanged };
    }

    // --- Assessment levels (CSV; key: classification + use + type + lower value + effective date) ---

    private static readonly string[] LevelColumns =
        ["ordinance-number", "ordinance-date", "classification", "actual-use", "property-type", "lower-value", "upper-value", "percentage", "effective-date", "source"];

    private async Task<VersionedPreview> LevelsAsync(byte[] bytes, string? fileSource, PackPending pending, VersionedPreview result, CancellationToken ct)
    {
        if (Table(bytes, LevelColumns, ["ordinance-number", "classification", "actual-use", "property-type", "lower-value", "percentage", "effective-date"], result)
                is not { } table)
        {
            return result;
        }
        var codes = await CodesAsync(ct);
        var existing = await db.AssessmentLevels.AsNoTracking()
            .Where(x => x.Status == WorkflowStatus.Draft || x.Status == WorkflowStatus.Approved).ToListAsync(ct);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var unchanged = 0;
        foreach (var row in table.Rows)
        {
            var line = row.Line;
            var ids = new Dictionary<string, Guid?>();
            var ok = Code(result, row, "classification", "classifications", true, codes, pending, ids)
                & Code(result, row, "actual-use", "actual-uses", true, codes, pending, ids)
                & Code(result, row, "property-type", "property-types", true, codes, pending, ids);
            DateOnly? ordinanceDate = null;
            if (row.Get("ordinance-date") is not null)
            {
                ok &= RowDate(result, row, "ordinance-date", out var d);
                ordinanceDate = d;
            }
            if (!ok || !Money(result, row, "lower-value", out var lower) || !OptionalMoney(result, row, "upper-value", out var upper)
                || !Money(result, row, "percentage", out var percent) || !RowDate(result, row, "effective-date", out var effective))
            {
                continue;
            }
            var values = new CreateAssessmentLevelRequest(row.Get("ordinance-number")!, ordinanceDate, ids["classification"] ?? Guid.NewGuid(),
                ids["actual-use"] ?? Guid.NewGuid(), ids["property-type"] ?? Guid.NewGuid(), lower, upper, percent, effective);
            if (!await ValidAsync(levelValidator, values, result, line, ct))
            {
                continue;
            }
            var key = string.Join("|", row.Get("classification"), row.Get("actual-use"), row.Get("property-type"),
                lower.ToString(CultureInfo.InvariantCulture), effective.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            if (!Unique(result, seen, key, line, "level"))
            {
                continue;
            }
            if (!PendingCode(row, pending) && existing.Any(l => l.ClassificationId == values.ClassificationId && l.ActualUseId == values.ActualUseId
                    && l.PropertyTypeId == values.PropertyTypeId && l.LowerValue == lower && l.UpperValue == upper && l.AssessmentPercentage == percent
                    && l.EffectiveDate == effective && l.OrdinanceNumber == values.OrdinanceNumber))
            {
                unchanged++;
                continue;
            }
            result.Versions.Add(new PlannedVersion(ContentFileKinds.AssessmentLevels, key,
                $"{row.Get("classification")} / {row.Get("actual-use")}: {percent.ToString(CultureInfo.InvariantCulture)}%", line,
                new PackAssessmentLevel(row.Get("classification")!, row.Get("actual-use")!, row.Get("property-type")!, values),
                [new ContentFieldChangeDto("percentage", null, percent.ToString(CultureInfo.InvariantCulture)),
                 new ContentFieldChangeDto("bracket", null, $"{lower.ToString(CultureInfo.InvariantCulture)} – {upper?.ToString(CultureInfo.InvariantCulture) ?? "up"}")],
                row.Get("source") ?? fileSource ?? ""));
        }
        return result with { Items = table.Rows.Count, Unchanged = unchanged };
    }

    // --- Import ---

    private async Task<Result<(string, Guid)>> CreateSmvAsync(PackSmv r, CancellationToken ct)
    {
        var ids = await db.Municipalities.Where(m => r.MunicipalityPsgcCodes.Contains(m.PsgcCode)).Select(m => m.Id).ToListAsync(ct);
        if (ids.Count != r.MunicipalityPsgcCodes.Count)
        {
            return Result.Failure<(string, Guid)>("CONTENT_PACK_IMPORT_FAILED", "A municipality of the SMV coverage was not found at import.");
        }
        return Map("Smv", await smvs.CreateSmvAsync(r.Request with { MunicipalityIds = ids }, ct), x => x.Id);
    }

    private async Task<Result<(string, Guid)>> CreateScheduleAsync(PackSmvSchedule r, CancellationToken ct)
    {
        var smvId = await db.Smvs.Where(x => x.OrdinanceNumber == r.SmvReference || x.CertificationReference == r.SmvReference)
            .Select(x => (Guid?)x.Id).FirstOrDefaultAsync(ct);
        var c = await CodesAsync(ct);
        Guid? Id(string lookup, string? code) => code is null ? null : c[lookup].GetValueOrDefault(code);
        Guid? barangayId = r.BarangayPsgc is null ? null
            : await db.Barangays.Where(x => x.PsgcCode == r.BarangayPsgc).Select(x => (Guid?)x.Id).FirstOrDefaultAsync(ct);
        if (smvId is null)
        {
            return Result.Failure<(string, Guid)>("CONTENT_PACK_IMPORT_FAILED", $"SMV {r.SmvReference} was not found at import.");
        }
        var values = r.Values with
        {
            ClassificationId = Id("classifications", r.Classification)!.Value,
            SubClassificationId = Id("sub-classifications", r.SubClassification),
            ActualUseId = Id("actual-uses", r.ActualUse),
            PropertyTypeId = Id("property-types", r.PropertyType)!.Value,
            ZoneId = Id("zones", r.Zone),
            ImprovementKindId = Id("improvement-kinds", r.ImprovementKind),
            BarangayId = barangayId,
        };
        return Map("SmvSchedule", await smvs.CreateScheduleAsync(smvId.Value, values, ct), x => x.Id);
    }

    private async Task<Result<(string, Guid)>> CreateLevelAsync(PackAssessmentLevel r, CancellationToken ct)
    {
        var c = await CodesAsync(ct);
        var values = r.Values with
        {
            ClassificationId = c["classifications"][r.Classification],
            ActualUseId = c["actual-uses"][r.ActualUse],
            PropertyTypeId = c["property-types"][r.PropertyType],
        };
        return Map("AssessmentLevel", await levels.CreateAsync(values, ct), x => x.Id);
    }

    // --- Helpers for CSV catalogues ---

    private async Task<Dictionary<string, Dictionary<string, Guid>>> CodesAsync(CancellationToken ct) => new()
    {
        ["classifications"] = await db.Classifications.AsNoTracking().ToDictionaryAsync(x => x.Code, x => x.Id, StringComparer.Ordinal, ct),
        ["sub-classifications"] = await db.SubClassifications.AsNoTracking().ToDictionaryAsync(x => x.Code, x => x.Id, StringComparer.Ordinal, ct),
        ["actual-uses"] = await db.ActualUses.AsNoTracking().ToDictionaryAsync(x => x.Code, x => x.Id, StringComparer.Ordinal, ct),
        ["property-types"] = await db.PropertyTypes.AsNoTracking().ToDictionaryAsync(x => x.Code, x => x.Id, StringComparer.Ordinal, ct),
        ["zones"] = await db.Zones.AsNoTracking().ToDictionaryAsync(x => x.Code, x => x.Id, StringComparer.Ordinal, ct),
        ["improvement-kinds"] = await db.ImprovementKinds.AsNoTracking().ToDictionaryAsync(x => x.Code, x => x.Id, StringComparer.Ordinal, ct),
    };

    private static readonly (string Column, string Lookup)[] CodeColumns =
    [
        ("classification", "classifications"), ("sub-classification", "sub-classifications"), ("actual-use", "actual-uses"),
        ("property-type", "property-types"), ("zone", "zones"), ("improvement-kind", "improvement-kinds"),
    ];

    /// <summary>Whether the row names a code only the pack adds (so it cannot match an existing record yet).</summary>
    private static bool PendingCode(CsvRow row, PackPending pending) =>
        CodeColumns.Any(c => row.Get(c.Column) is { } code && pending.Lookups.TryGetValue(c.Lookup, out var set) && set.Contains(code));

    /// <summary>Resolves a code column to an id (null when blank, or when the code is one the pack adds).</summary>
    private static bool Code(VersionedPreview result, CsvRow row, string column, string lookup, bool required,
        Dictionary<string, Dictionary<string, Guid>> codes, PackPending pending, Dictionary<string, Guid?> ids)
    {
        var code = row.Get(column);
        ids[column] = null;
        if (code is null)
        {
            if (required)
            {
                result.Issues.Add(Error("VALUE_MISSING", $"Line {row.Line}: {column} is required.", row.Line, column));
                return false;
            }
            return true;
        }
        if (codes[lookup].TryGetValue(code, out var id))
        {
            ids[column] = id;
            return true;
        }
        if (pending.Lookups.TryGetValue(lookup, out var added) && added.Contains(code))
        {
            return true;
        }
        result.Issues.Add(Error("CODE_UNKNOWN", $"Line {row.Line}: {column} {code} is neither in PRIME nor added by this pack ({lookup}).", row.Line, column));
        return false;
    }

    private static CsvTable? Table(byte[] bytes, string[] columns, string[] required, VersionedPreview result)
    {
        var table = ContentPackCsv.Parse(bytes);
        if (table.Error is not null)
        {
            result.Issues.Add(Error("CSV_INVALID", table.Error, table.ErrorLine));
            return null;
        }
        var unknown = table.Columns.Except(columns).ToList();
        var missing = required.Except(table.Columns).ToList();
        if (unknown.Count > 0 || missing.Count > 0)
        {
            result.Issues.Add(Error("CSV_COLUMNS", string.Join(" ", new[]
            {
                unknown.Count > 0 ? $"Unknown column {string.Join(", ", unknown)}." : null,
                missing.Count > 0 ? $"Missing column {string.Join(", ", missing)}." : null,
                $"Columns: {string.Join(", ", columns)}.",
            }.Where(s => s is not null)), 1));
            return null;
        }
        return table;
    }

    private static bool Money(VersionedPreview result, CsvRow row, string column, out decimal value)
    {
        if (decimal.TryParse(row.Get(column), NumberStyles.Number, CultureInfo.InvariantCulture, out value))
        {
            return true;
        }
        result.Issues.Add(Error("NUMBER_INVALID", $"Line {row.Line}: {column} must be a number written like 1234.50; got '{row.Get(column)}'.", row.Line, column));
        return false;
    }

    private static bool OptionalMoney(VersionedPreview result, CsvRow row, string column, out decimal? value)
    {
        value = null;
        if (row.Get(column) is null)
        {
            return true;
        }
        if (!Money(result, row, column, out var v))
        {
            return false;
        }
        value = v;
        return true;
    }

    private static bool RowDate(VersionedPreview result, CsvRow row, string column, out DateOnly value)
    {
        if (DateOnly.TryParseExact(row.Get(column), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out value))
        {
            return true;
        }
        result.Issues.Add(Error("DATE_INVALID", $"Line {row.Line}: {column} must be a date written yyyy-MM-dd; got '{row.Get(column)}'.", row.Line, column));
        return false;
    }
}
