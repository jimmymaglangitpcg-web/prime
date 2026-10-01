using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Prime.Application.Common;
using Prime.Application.Features.Valuation;
using Prime.Domain.Enums;

namespace Prime.Application.Features.ContentPacks;

/// <summary>
/// Step L1-6 (docs/analysis/valuation-foundation.md §4.6): exchange rates and price indices for machinery,
/// as CSV. Each row becomes a Draft observation for a second user to approve. A row whose value PRIME
/// already holds (approved or awaiting approval) is unchanged; an approved observation with another
/// value is refused, since an approved rate or index is never edited.
/// </summary>
public sealed partial class ContentPackVersionedContent
{
    private static readonly string[] ExchangeRateColumns = ["currency", "rate-date", "pesos-per-unit", "source", "remarks"];
    private static readonly string[] PriceIndexColumns = ["series", "year", "value", "source", "remarks"];

    private async Task<VersionedPreview> ExchangeRatesAsync(byte[] bytes, string? fileSource, VersionedPreview result, CancellationToken ct)
    {
        if (Table(bytes, ExchangeRateColumns, ["currency", "rate-date", "pesos-per-unit"], result) is not { } table)
        {
            return result;
        }
        var existing = await db.ExchangeRates.AsNoTracking().Where(x => x.Status == WorkflowStatus.Draft || x.Status == WorkflowStatus.Approved).ToListAsync(ct);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var unchanged = 0;
        foreach (var row in table.Rows)
        {
            var line = row.Line;
            var currency = row.Get("currency")!;
            if (!RowDate(result, row, "rate-date", out var date) || !Money(result, row, "pesos-per-unit", out var rate))
            {
                continue;
            }
            var source = row.Get("source") ?? fileSource ?? "";
            if (currency.Length != 3 || !currency.All(char.IsAsciiLetterUpper) || rate <= 0m || source.Length is 0 or > 300)
            {
                result.Issues.Add(Error("VALIDATION_FAILED", $"Line {line}: currency is a 3-letter ISO 4217 code, pesos-per-unit is above 0 and a source (max 300) is given.", line));
                continue;
            }
            var key = $"{currency} {date:yyyy-MM-dd}";
            if (!Unique(result, seen, key, line, "currency/rate-date"))
            {
                continue;
            }
            var same = existing.Where(x => x.Currency == currency && x.RateDate == date).ToList();
            if (same.Any(x => x.PesosPerUnit == rate))
            {
                unchanged++;
                continue;
            }
            if (same.FirstOrDefault(x => x.Status == WorkflowStatus.Approved) is { } approved)
            {
                result.Issues.Add(Error("EXCHANGE_RATE_EXISTS",
                    $"Line {line}: PRIME holds an approved {key} rate of {approved.PesosPerUnit.ToString(CultureInfo.InvariantCulture)}; an approved rate is never edited.", line));
                continue;
            }
            result.Versions.Add(new PlannedVersion(ContentFileKinds.ExchangeRates, key, $"{key}: {rate.ToString(CultureInfo.InvariantCulture)} pesos", line,
                new CreateExchangeRateRequest(currency, date, rate, source, row.Get("remarks")),
                [new ContentFieldChangeDto("pesosPerUnit", null, rate.ToString(CultureInfo.InvariantCulture))], source));
        }
        return result with { Items = table.Rows.Count, Unchanged = unchanged };
    }

    private async Task<VersionedPreview> PriceIndicesAsync(byte[] bytes, string? fileSource, VersionedPreview result, CancellationToken ct)
    {
        if (Table(bytes, PriceIndexColumns, ["series", "year", "value"], result) is not { } table)
        {
            return result;
        }
        var existing = await db.PriceIndices.AsNoTracking().Where(x => x.Status == WorkflowStatus.Draft || x.Status == WorkflowStatus.Approved).ToListAsync(ct);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var unchanged = 0;
        foreach (var row in table.Rows)
        {
            var line = row.Line;
            var series = row.Get("series")!;
            if (!int.TryParse(row.Get("year"), NumberStyles.None, CultureInfo.InvariantCulture, out var year) || year is < 1900 or > 2200)
            {
                result.Issues.Add(Error("NUMBER_INVALID", $"Line {line}: year must be a year from 1900 to 2200; got '{row.Get("year")}'.", line, "year"));
                continue;
            }
            if (!Money(result, row, "value", out var value))
            {
                continue;
            }
            var source = row.Get("source") ?? fileSource ?? "";
            if (series.Length > 30 || value <= 0m || source.Length is 0 or > 300)
            {
                result.Issues.Add(Error("VALIDATION_FAILED", $"Line {line}: series (max 30), a value above 0 and a source (max 300) are required.", line));
                continue;
            }
            var key = $"{series} {year}";
            if (!Unique(result, seen, key, line, "series/year"))
            {
                continue;
            }
            var same = existing.Where(x => x.Series == series && x.Year == year).ToList();
            if (same.Any(x => x.Value == value))
            {
                unchanged++;
                continue;
            }
            if (same.FirstOrDefault(x => x.Status == WorkflowStatus.Approved) is { } approved)
            {
                result.Issues.Add(Error("PRICE_INDEX_EXISTS",
                    $"Line {line}: PRIME holds an approved {key} index of {approved.Value.ToString(CultureInfo.InvariantCulture)}; an approved index is never edited.", line));
                continue;
            }
            result.Versions.Add(new PlannedVersion(ContentFileKinds.PriceIndices, key, $"{key}: {value.ToString(CultureInfo.InvariantCulture)}", line,
                new CreatePriceIndexRequest(series, year, value, source, row.Get("remarks")),
                [new ContentFieldChangeDto("value", null, value.ToString(CultureInfo.InvariantCulture))], source));
        }
        return result with { Items = table.Rows.Count, Unchanged = unchanged };
    }
}
