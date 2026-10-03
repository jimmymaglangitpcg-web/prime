using System.Globalization;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Prime.Application.Common;
using Prime.Application.Common.Interfaces;
using Prime.Application.Features.ContentPacks;
using Prime.Domain.Entities.MarketData;
using Prime.Domain.Enums;

namespace Prime.Application.Features.MarketData;

/// <param name="Summary">What the row records, for the preview.</param>
public sealed record MarketImportRowDto(int Line, bool Valid, IReadOnlyList<string> Errors, string? Summary);

/// <param name="Fingerprint">Pass it back to import exactly the file previewed.</param>
/// <param name="ImportedCount">Set when imported.</param>
public sealed record MarketImportResultDto(string Fingerprint, int RowCount, int ValidCount, IReadOnlyList<MarketImportRowDto> Rows, string? Batch, int ImportedCount);

public interface IMarketDataImportService
{
    /// <summary>Validates every row; nothing is saved.</summary>
    Task<Result<MarketImportResultDto>> PreviewAsync(byte[] content, CancellationToken cancellationToken = default);
    /// <summary>Imports the file previewed under <paramref name="fingerprint"/>, all rows in one transaction; refused if any row is invalid.</summary>
    Task<Result<MarketImportResultDto>> ImportAsync(byte[] content, string fingerprint, CancellationToken cancellationToken = default);
}

/// <summary>
/// CSV import of market transactions (docs/analysis/smv-preparation-general-revision.md §4.1, Q4), following CLAUDE.md
/// §60: upload, validate, preview with each row's errors, confirm, import, audit. Invalid data is never imported: one
/// bad row refuses the file. Imported rows are Unreviewed and carry the batch name. Columns (header, any order):
/// <c>transaction_date</c>, <c>municipality</c> (PSGC code or exact name), <c>consideration</c> required; <c>source</c>,
/// <c>conveyance_mode</c>, <c>document_reference</c>, <c>document_file_number</c>, <c>grantor</c>, <c>grantee</c>,
/// <c>grantee_address</c>, <c>barangay</c> (PSGC code or name), <c>location</c>, <c>pin</c>, <c>td_number</c>,
/// <c>lot_number</c>, <c>previous_title</c>, <c>new_title</c>, <c>conveys</c> (land, building or both; default land),
/// <c>classification</c>, <c>sub_class</c>, <c>actual_use</c>, <c>building_type</c>, <c>structural_type</c> (codes),
/// <c>land_area</c>, <c>land_area_unit</c> (sqm or ha), <c>floor_area</c>, <c>land_consideration</c>, <c>remarks</c>.
/// </summary>
public sealed class MarketDataImportService(IApplicationDbContext db, IClock clock, IJurisdiction jurisdiction) : IMarketDataImportService
{
    public const int MaxRows = 5000;

    private static readonly HashSet<string> Known =
    [
        "transaction_date", "municipality", "consideration", "source", "conveyance_mode", "document_reference", "document_file_number", "grantor",
        "grantee", "grantee_address", "barangay", "location", "pin", "td_number", "lot_number", "previous_title", "new_title", "conveys",
        "classification", "sub_class", "actual_use", "building_type", "structural_type", "land_area", "land_area_unit", "floor_area",
        "land_consideration", "remarks",
    ];

    public Task<Result<MarketImportResultDto>> PreviewAsync(byte[] content, CancellationToken cancellationToken = default) =>
        RunAsync(content, null, cancellationToken);

    public Task<Result<MarketImportResultDto>> ImportAsync(byte[] content, string fingerprint, CancellationToken cancellationToken = default) =>
        RunAsync(content, fingerprint ?? string.Empty, cancellationToken);

    private async Task<Result<MarketImportResultDto>> RunAsync(byte[] content, string? confirmFingerprint, CancellationToken ct)
    {
        var fingerprint = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
        if (confirmFingerprint is not null && confirmFingerprint != fingerprint)
        {
            return Result.Failure<MarketImportResultDto>("IMPORT_FILE_CHANGED", "The file differs from the one previewed: preview it again.");
        }
        var table = ContentPackCsv.Parse(content);
        if (table.Error is { } parseError)
        {
            return Result.Failure<MarketImportResultDto>("IMPORT_FILE_INVALID", $"Line {table.ErrorLine}: {parseError}");
        }
        var unknown = table.Columns.Where(c => !Known.Contains(c)).ToList();
        var missing = new[] { "transaction_date", "municipality", "consideration" }.Where(c => !table.Columns.Contains(c)).ToList();
        if (unknown.Count > 0 || missing.Count > 0)
        {
            return Result.Failure<MarketImportResultDto>("IMPORT_FILE_INVALID",
                string.Join(" ", new[] { missing.Count > 0 ? $"Missing columns: {string.Join(", ", missing)}." : null,
                    unknown.Count > 0 ? $"Unknown columns: {string.Join(", ", unknown)}." : null }.Where(x => x is not null)));
        }
        if (table.Rows.Count == 0 || table.Rows.Count > MaxRows)
        {
            return Result.Failure<MarketImportResultDto>("IMPORT_FILE_INVALID", $"The file must hold between 1 and {MaxRows} rows.");
        }

        var lookups = await LookupsAsync(ct);
        var rows = new List<MarketImportRowDto>();
        var parsed = new List<MarketTransaction>();
        var seen = new HashSet<(Guid, DateOnly, string?, decimal)>();
        foreach (var row in table.Rows)
        {
            var errors = new List<string>();
            var request = ToRequest(row, lookups, errors);
            var entity = new MarketTransaction();
            if (request is not null)
            {
                if (await MarketTransactionRules.ApplyAsync(db, clock, jurisdiction, entity, request, ct) is { } error)
                {
                    errors.Add(error.Message);
                }
                else
                {
                    var key = (entity.MunicipalityId, entity.TransactionDate, entity.DocumentReference, entity.Consideration);
                    if (!seen.Add(key))
                    {
                        errors.Add("Repeats an earlier row (same city/municipality, date, document and consideration).");
                    }
                    else if (await db.MarketTransactions.AnyAsync(x => x.CancelledAt == null && x.MunicipalityId == key.Item1 && x.TransactionDate == key.Item2
                                 && x.DocumentReference == key.Item3 && x.Consideration == key.Item4, ct))
                    {
                        errors.Add("Already recorded (same city/municipality, date, document and consideration).");
                    }
                }
            }
            rows.Add(new MarketImportRowDto(row.Line, errors.Count == 0, errors, errors.Count == 0
                ? $"{entity.TransactionDate:yyyy-MM-dd} · {entity.Consideration:N2}{(entity.LandUnitPrice is { } u ? $" · {u:N2}/{(entity.LandAreaUnit == AreaMeasure.Hectare ? "ha" : "sqm")}" : "")}"
                : null));
            if (errors.Count == 0)
            {
                parsed.Add(entity);
            }
        }
        var valid = rows.Count(r => r.Valid);
        if (confirmFingerprint is null)
        {
            return Result.Success(new MarketImportResultDto(fingerprint, rows.Count, valid, rows, null, 0));
        }
        if (valid != rows.Count)
        {
            return Result.Failure<MarketImportResultDto>("IMPORT_HAS_ERRORS", $"{rows.Count - valid} row(s) have errors; correct the file and preview it again. Nothing was imported.");
        }
        var batch = $"IMPORT-{clock.UtcNow:yyyyMMdd-HHmmss}-{fingerprint[..8]}";
        foreach (var entity in parsed)
        {
            entity.ImportBatch = batch;
            db.MarketTransactions.Add(entity);
        }
        var ownsTransaction = db.Database.CurrentTransaction is null;
        await using var transaction = ownsTransaction ? await db.Database.BeginTransactionAsync(ct) : null;
        await db.SaveChangesAsync(ct);
        if (transaction is not null)
        {
            await transaction.CommitAsync(ct);
        }
        return Result.Success(new MarketImportResultDto(fingerprint, rows.Count, valid, rows, batch, parsed.Count));
    }

    private sealed record Lookups(
        Dictionary<string, Guid> Municipalities, List<(Guid Id, Guid MunicipalityId, string Name, string Psgc)> Barangays,
        Dictionary<string, Guid> Modes, Dictionary<string, Guid> Classes, Dictionary<string, Guid> SubClasses, Dictionary<string, Guid> Uses,
        Dictionary<string, Guid> BuildingTypes, Dictionary<string, Guid> StructuralTypes);

    private async Task<Lookups> LookupsAsync(CancellationToken ct)
    {
        var municipalities = await db.Municipalities.AsNoTracking().Select(x => new { x.Id, x.Name, x.PsgcCode }).ToListAsync(ct);
        var map = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        foreach (var m in municipalities)
        {
            if (!string.IsNullOrEmpty(m.PsgcCode)) map.TryAdd(m.PsgcCode, m.Id);
            map.TryAdd(m.Name, m.Id);
        }
        var barangays = (await db.Barangays.AsNoTracking().Select(x => new { x.Id, x.MunicipalityId, x.Name, x.PsgcCode }).ToListAsync(ct))
            .Select(x => (x.Id, x.MunicipalityId, x.Name, x.PsgcCode)).ToList();
        static Dictionary<string, Guid> Codes(IEnumerable<(string Code, Guid Id)> rows) =>
            rows.GroupBy(x => x.Code, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase);
        return new Lookups(map, barangays,
            Codes((await db.ConveyanceModes.AsNoTracking().Where(x => x.IsActive).Select(x => new { x.Code, x.Id }).ToListAsync(ct)).Select(x => (x.Code, x.Id))),
            Codes((await db.Classifications.AsNoTracking().Select(x => new { x.Code, x.Id }).ToListAsync(ct)).Select(x => (x.Code, x.Id))),
            Codes((await db.SubClassifications.AsNoTracking().Select(x => new { x.Code, x.Id }).ToListAsync(ct)).Select(x => (x.Code, x.Id))),
            Codes((await db.ActualUses.AsNoTracking().Select(x => new { x.Code, x.Id }).ToListAsync(ct)).Select(x => (x.Code, x.Id))),
            Codes((await db.BuildingTypes.AsNoTracking().Select(x => new { x.Code, x.Id }).ToListAsync(ct)).Select(x => (x.Code, x.Id))),
            Codes((await db.StructuralTypes.AsNoTracking().Select(x => new { x.Code, x.Id }).ToListAsync(ct)).Select(x => (x.Code, x.Id))));
    }

    private static SaveMarketTransactionRequest? ToRequest(CsvRow row, Lookups l, List<string> errors)
    {
        DateOnly date = default;
        if (!DateOnly.TryParseExact(row.Get("transaction_date") ?? "", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
        {
            errors.Add("transaction_date: expected yyyy-MM-dd.");
        }
        Guid municipalityId = default;
        if (row.Get("municipality") is not { } mun || !l.Municipalities.TryGetValue(mun, out municipalityId))
        {
            errors.Add("municipality: not a known PSGC code or name.");
        }
        Guid? barangayId = null;
        if (row.Get("barangay") is { } brgy && municipalityId != default)
        {
            var match = l.Barangays.Where(b => b.MunicipalityId == municipalityId
                && (string.Equals(b.Psgc, brgy, StringComparison.OrdinalIgnoreCase) || string.Equals(b.Name, brgy, StringComparison.OrdinalIgnoreCase))).ToList();
            if (match.Count == 1) barangayId = match[0].Id;
            else errors.Add($"barangay: {(match.Count == 0 ? "not found in the city/municipality" : "ambiguous name; use the PSGC code")}.");
        }
        var consideration = Number(row, "consideration", errors) ?? 0m;
        if (row.Get("consideration") is null) errors.Add("consideration: required.");
        var source = MarketDataSource.RegistryAbstract;
        if (row.Get("source") is { } s && !Enum.TryParse(s, true, out source)) errors.Add("source: unknown value.");
        var (land, building) = (row.Get("conveys")?.ToLowerInvariant()) switch
        {
            null or "land" => (true, false),
            "building" => (false, true),
            "both" => (true, true),
            _ => (true, false),
        };
        if (row.Get("conveys") is { } conveys && conveys.ToLowerInvariant() is not ("land" or "building" or "both")) errors.Add("conveys: land, building or both.");
        var unit = AreaMeasure.SquareMetre;
        if (row.Get("land_area_unit")?.ToLowerInvariant() is { } u)
        {
            if (u is "ha" or "hectare") unit = AreaMeasure.Hectare;
            else if (u is not ("sqm" or "m2")) errors.Add("land_area_unit: sqm or ha.");
        }
        Guid? Code(string column, Dictionary<string, Guid> map)
        {
            if (row.Get(column) is not { } code) return null;
            if (map.TryGetValue(code, out var id)) return id;
            errors.Add($"{column}: unknown code {code}.");
            return null;
        }
        var request = new SaveMarketTransactionRequest(
            source, Code("conveyance_mode", l.Modes), date, row.Get("document_reference"), row.Get("document_file_number"),
            row.Get("grantor"), row.Get("grantee"), row.Get("grantee_address"), municipalityId, barangayId, row.Get("location"), null,
            row.Get("pin"), row.Get("td_number"), row.Get("lot_number"), row.Get("previous_title"), row.Get("new_title"),
            land, building, Code("classification", l.Classes), Code("sub_class", l.SubClasses), Code("actual_use", l.Uses),
            Code("building_type", l.BuildingTypes), Code("structural_type", l.StructuralTypes),
            Number(row, "land_area", errors), unit, Number(row, "floor_area", errors), consideration, Number(row, "land_consideration", errors), row.Get("remarks"));
        return errors.Count == 0 ? request : null;
    }

    private static decimal? Number(CsvRow row, string column, List<string> errors)
    {
        if (row.Get(column) is not { } text) return null;
        if (decimal.TryParse(text.Replace(",", ""), NumberStyles.Number, CultureInfo.InvariantCulture, out var value)) return value;
        errors.Add($"{column}: not a number.");
        return null;
    }
}
