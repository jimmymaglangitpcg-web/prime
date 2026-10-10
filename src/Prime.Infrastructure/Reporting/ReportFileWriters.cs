using System.Globalization;
using System.Text;
using MiniExcelLibs;
using MiniExcelLibs.Attributes;
using MiniExcelLibs.OpenXml;
using Prime.Application.Features.Reports;

namespace Prime.Infrastructure.Reporting;

/// <summary>
/// A report as CSV (docs/analysis/reporting.md §4.1): UTF-8 with a byte-order mark so Excel reads it correctly, the column
/// titles, then one line per row, written as it goes. A CSV is data for reuse: no header block, totals or notes (the Excel
/// file has them). Numbers use the invariant culture; dates are ISO.
/// </summary>
public sealed class CsvReportWriter : IReportFileWriter
{
    public ReportFormat Format => ReportFormat.Csv;
    public string ContentType => "text/csv; charset=utf-8";
    public string Extension => "csv";

    public async Task WriteAsync(ReportDocument document, Stream output, CancellationToken cancellationToken)
    {
        await using var writer = new StreamWriter(output, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true), bufferSize: 64 * 1024, leaveOpen: true)
        {
            NewLine = "\r\n",
        };
        await writer.WriteLineAsync(string.Join(",", document.Columns.Select(c => Quote(c.Title))));
        foreach (var row in document.Rows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await writer.WriteLineAsync(string.Join(",", document.Columns.Select((c, i) => Cell(c, i < row.Length ? row[i] : null))));
        }
        await writer.FlushAsync(cancellationToken);
    }

    private static string Cell(ReportColumn column, object? value) => value switch
    {
        null => string.Empty,
        DateOnly d => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        // Money to the centavo, areas without trailing zeros: the same text whatever scale the database returned.
        decimal m when column.Type == ReportColumnType.Money => m.ToString("0.00", CultureInfo.InvariantCulture),
        decimal a when column.Type == ReportColumnType.Area => a.ToString("0.####", CultureInfo.InvariantCulture),
        IFormattable f when column.Type != ReportColumnType.Text => f.ToString(null, CultureInfo.InvariantCulture),
        _ => Quote(ReportCells.Neutralize(value.ToString() ?? string.Empty)),
    };

    /// <summary>RFC 4180: a field with a comma, quote or line break is quoted, its quotes doubled.</summary>
    private static string Quote(string text) =>
        text.IndexOfAny([',', '"', '\r', '\n']) >= 0 ? $"\"{text.Replace("\"", "\"\"")}\"" : text;
}

/// <summary>
/// A report as an Excel workbook (Q1: MiniExcel), one sheet: the header block (LGU, office, title, parameters, when run and
/// by whom), the column titles, the rows, the totals and the notes. Numbers and dates are written as typed cells. Built in
/// memory (a download holds at most <c>Reports:SyncRowLimit</c> rows) and copied to the response asynchronously.
/// </summary>
public sealed class ExcelReportWriter : IReportFileWriter
{
    public ReportFormat Format => ReportFormat.Xlsx;
    public string ContentType => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    public string Extension => "xlsx";

    public async Task WriteAsync(ReportDocument document, Stream output, CancellationToken cancellationToken)
    {
        var keys = document.Columns.Select((_, i) => ColumnName(i)).ToArray();
        using var buffer = new MemoryStream();
        var configuration = new OpenXmlConfiguration
        {
            TableStyles = TableStyles.None, AutoFilter = false, FreezeRowCount = 0,
            // Amounts and areas with thousands separators and two decimals, counts whole; text wide enough to read.
            DynamicColumns = [.. document.Columns.Select((c, i) => new DynamicExcelColumn(keys[i])
            {
                Width = c.Type switch { ReportColumnType.Text => 28, ReportColumnType.Date => 12, _ => 18 },
                Format = c.Type switch
                {
                    ReportColumnType.Money => "#,##0.00",
                    ReportColumnType.Area => "#,##0.00",
                    ReportColumnType.Integer => "#,##0",
                    ReportColumnType.Date => "yyyy-mm-dd",
                    _ => null,
                },
            })],
        };
        await MiniExcel.SaveAsAsync(buffer, Sheet(document, keys), printHeader: false, sheetName: "Report", excelType: ExcelType.XLSX,
            configuration: configuration, cancellationToken: cancellationToken);
        buffer.Position = 0;
        await buffer.CopyToAsync(output, cancellationToken);
    }

    private static IEnumerable<Dictionary<string, object?>> Sheet(ReportDocument document, string[] keys)
    {
        Dictionary<string, object?> Line(params object?[] cells) =>
            keys.Select((k, i) => (k, v: i < cells.Length ? cells[i] : null)).ToDictionary(x => x.k, x => x.v);
        object? Value(ReportColumn column, object? value) => value switch
        {
            DateOnly d => d.ToDateTime(TimeOnly.MinValue),
            string s when column.Type == ReportColumnType.Text => ReportCells.Neutralize(s),
            _ => value,
        };

        foreach (var line in document.HeaderLines)
        {
            yield return Line(line);
        }
        yield return Line();
        yield return Line([.. document.Columns.Select(c => (object?)c.Title)]);
        foreach (var row in document.Rows)
        {
            yield return Line([.. document.Columns.Select((c, i) => Value(c, i < row.Length ? row[i] : null))]);
        }
        if (document.Totals is { } totals)
        {
            yield return Line([.. document.Columns.Select((c, i) => Value(c, i < totals.Length ? totals[i] : null))]);
        }
        if (document.Notes.Count > 0)
        {
            yield return Line();
            foreach (var note in document.Notes)
            {
                yield return Line(note);
            }
        }
    }

    /// <summary>A, B, … Z, AA, AB …: the sheet's column letters, used as row keys.</summary>
    private static string ColumnName(int index)
    {
        var name = string.Empty;
        for (var n = index + 1; n > 0; n = (n - 1) / 26)
        {
            name = (char)('A' + (n - 1) % 26) + name;
        }
        return name;
    }
}

public static class ReportCells
{
    /// <summary>
    /// A text cell starting with =, +, -, @ or a control character would be read by a spreadsheet as a formula (CSV/formula
    /// injection, OWASP): it is prefixed with an apostrophe.
    /// </summary>
    public static string Neutralize(string text) =>
        text.Length > 0 && text[0] is '=' or '+' or '-' or '@' or '\t' or '\r' ? "'" + text : text;
}
