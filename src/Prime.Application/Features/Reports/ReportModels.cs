namespace Prime.Application.Features.Reports;

/// <summary>How a column's values are typed in CSV and Excel and formatted on screen.</summary>
public enum ReportColumnType
{
    Text,
    Integer,
    Money,
    /// <summary>Square metres.</summary>
    Area,
    Date,
}

/// <summary>A parameter a report takes (docs/analysis/reporting.md §4.1); the screen shows only those the report lists.</summary>
public enum ReportParameter
{
    AsOf,
    Municipality,
    Barangay,
}

public sealed record ReportColumn(string Key, string Title, ReportColumnType Type);

public sealed record ReportDefinitionDto(
    string Code, string Title, string Group, string Description, IReadOnlyList<ReportParameter> Parameters, IReadOnlyList<ReportColumn> Columns);

/// <summary>The parameters of a run. The as-of date defaults to today in the LGU's time zone.</summary>
public sealed record ReportRunRequest
{
    public DateOnly? AsOf { get; init; }
    public Guid? MunicipalityId { get; init; }
    public Guid? BarangayId { get; init; }
}

public sealed record ReportPreviewRequest
{
    public ReportRunRequest Parameters { get; init; } = new();
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
}

/// <summary>One page of a report on screen. <see cref="Totals"/> is the whole report's, not the page's.</summary>
public sealed record ReportPreviewDto(
    string Code, string Title, IReadOnlyList<ReportColumn> Columns, IReadOnlyList<object?[]> Rows, object?[]? Totals,
    int TotalRows, int Page, int PageSize, IReadOnlyList<string> Notes, IReadOnlyList<string> ParameterLines, int SyncRowLimit);

/// <summary>A run's validated parameters, with the names the header block prints.</summary>
public sealed record ReportScope(DateOnly AsOf, Guid? MunicipalityId, Guid? BarangayId, string? MunicipalityName, string? BarangayName)
{
    /// <summary>The parameters as the header block and the audit row print them.</summary>
    public IReadOnlyList<string> Lines(IReadOnlyList<ReportParameter> used)
    {
        var lines = new List<string>();
        if (used.Contains(ReportParameter.AsOf))
        {
            lines.Add($"As of: {AsOf:yyyy-MM-dd}");
        }
        if (used.Contains(ReportParameter.Municipality))
        {
            lines.Add($"Municipality: {MunicipalityName ?? "All in your jurisdiction"}");
        }
        if (used.Contains(ReportParameter.Barangay) && BarangayName is not null)
        {
            lines.Add($"Barangay: {BarangayName}");
        }
        return lines;
    }
}

/// <summary>
/// Which rows a run returns: <see cref="Take"/> rows from <see cref="Skip"/>. A report whose row count exceeds
/// <see cref="MaxTotal"/> returns its count only, without reading the rows (an export over the synchronous limit, Q3).
/// </summary>
public sealed record ReportWindow(int Skip, int Take, int? MaxTotal = null);

/// <summary>A run's output. <see cref="Totals"/> is aligned with the columns (null cells left blank).</summary>
public sealed record ReportRows(IReadOnlyList<object?[]> Rows, int TotalRows, object?[]? Totals, IReadOnlyList<string> Notes);

/// <summary>
/// One report (docs/analysis/reporting.md §4.1, Q4): a query in code returning rows of typed columns. Reports read through
/// the database's jurisdiction filters like every screen, and aggregate in the database (CLAUDE.md §71).
/// </summary>
public interface IReport
{
    string Code { get; }
    string Title { get; }
    string Group { get; }
    string Description { get; }
    IReadOnlyList<ReportParameter> Parameters { get; }
    IReadOnlyList<ReportColumn> Columns { get; }
    Task<ReportRows> RunAsync(ReportScope scope, ReportWindow window, CancellationToken cancellationToken);
}

/// <summary>A whole report for a file: the header block's lines, the columns and every row.</summary>
public sealed record ReportDocument(
    string Title, IReadOnlyList<string> HeaderLines, IReadOnlyList<ReportColumn> Columns, IReadOnlyList<object?[]> Rows, object?[]? Totals,
    IReadOnlyList<string> Notes);

public enum ReportFormat
{
    Csv,
    Xlsx,
}

/// <summary>Writes a report to a file format (Infrastructure: CSV, and Excel with MiniExcel, Q1).</summary>
public interface IReportFileWriter
{
    ReportFormat Format { get; }
    string ContentType { get; }
    string Extension { get; }
    Task WriteAsync(ReportDocument document, Stream output, CancellationToken cancellationToken);
}

/// <summary>
/// A download ready to write: its rows are read and its EXPORT row written before the response starts, so a refusal or a
/// database error still reaches the user as an error, not as a broken file.
/// </summary>
public sealed record ReportExport(string FileName, string ContentType, Func<Stream, CancellationToken, Task> WriteAsync);

public sealed class ReportsOptions
{
    public const string SectionName = "Reports";

    /// <summary>The most rows a download writes in the request (Q3); a larger report runs as a background job (step R5).</summary>
    public int SyncRowLimit { get; set; } = 20_000;

    /// <summary>Days a background report's file is kept (step R5).</summary>
    public int FileRetentionDays { get; set; } = 7;
}
