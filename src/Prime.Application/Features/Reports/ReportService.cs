using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Prime.Application.Common;
using Prime.Application.Common.Interfaces;
using Prime.Application.Features.Audit;
using Prime.Application.Features.Offices;

namespace Prime.Application.Features.Reports;

public interface IReportService
{
    IReadOnlyList<ReportDefinitionDto> List();
    Task<Result<ReportPreviewDto>> PreviewAsync(string code, ReportPreviewRequest request, CancellationToken cancellationToken = default);
    Task<Result<ReportExport>> ExportAsync(string code, ReportRunRequest request, ReportFormat format, CancellationToken cancellationToken = default);
}

/// <summary>
/// The report catalogue and its runs (docs/analysis/reporting.md §4.1): the reports a user may run, a page of one on screen,
/// and the whole of one as CSV or Excel. A download is limited to <see cref="ReportsOptions.SyncRowLimit"/> rows (Q3); each
/// writes an EXPORT audit row with the report, its parameters, the format and the row count (P12-3).
/// </summary>
public sealed class ReportService(
    IEnumerable<IReport> reports,
    IEnumerable<IReportFileWriter> writers,
    IApplicationDbContext db,
    IJurisdiction jurisdiction,
    IClock clock,
    IOfficeContext office,
    ICurrentUserService currentUser,
    ISecurityEventLog events,
    IOptions<LguOptions> lgu,
    IOptions<ReportsOptions> options) : IReportService
{
    /// <summary>The audit trail's table name for report downloads.</summary>
    public const string AuditTable = "Reports";

    private const int MaxPageSize = 200;

    public IReadOnlyList<ReportDefinitionDto> List() => reports
        .Select(r => new ReportDefinitionDto(r.Code, r.Title, r.Group, r.Description, r.Parameters, r.Columns))
        .ToList();

    public async Task<Result<ReportPreviewDto>> PreviewAsync(string code, ReportPreviewRequest request, CancellationToken cancellationToken = default)
    {
        if (Find(code) is not { } report)
        {
            return Result.Failure<ReportPreviewDto>("REPORT_NOT_FOUND", "There is no such report.");
        }
        if (request.Page < 1 || request.PageSize is < 1 or > MaxPageSize)
        {
            return Result.Failure<ReportPreviewDto>("VALIDATION_FAILED", $"The page starts at 1 and holds 1 to {MaxPageSize} rows.");
        }
        var scope = await ScopeAsync(report, request.Parameters, cancellationToken);
        if (!scope.IsSuccess)
        {
            return Result.Failure<ReportPreviewDto>(scope.Code!, scope.Message!);
        }
        var rows = await report.RunAsync(scope.Value, new ReportWindow((request.Page - 1) * request.PageSize, request.PageSize), cancellationToken);
        var lines = scope.Value.Lines(report.Parameters);
        return Result.Success(new ReportPreviewDto(report.Code, report.Title, report.Columns, rows.Rows, rows.Totals, rows.TotalRows,
            request.Page, request.PageSize, rows.Notes, lines, options.Value.SyncRowLimit)
        {
            HeaderLines = request.WithHeader ? await HeaderAsync(report, lines, cancellationToken) : null,
        });
    }

    public async Task<Result<ReportExport>> ExportAsync(string code, ReportRunRequest request, ReportFormat format, CancellationToken cancellationToken = default)
    {
        if (Find(code) is not { } report)
        {
            return Result.Failure<ReportExport>("REPORT_NOT_FOUND", "There is no such report.");
        }
        if (writers.FirstOrDefault(w => w.Format == format) is not { } writer)
        {
            return Result.Failure<ReportExport>("VALIDATION_FAILED", "The format must be csv or xlsx.");
        }
        var scope = await ScopeAsync(report, request, cancellationToken);
        if (!scope.IsSuccess)
        {
            return Result.Failure<ReportExport>(scope.Code!, scope.Message!);
        }
        var limit = options.Value.SyncRowLimit;
        var rows = await report.RunAsync(scope.Value, new ReportWindow(0, limit, limit), cancellationToken);
        if (rows.TotalRows > limit)
        {
            return Result.Failure<ReportExport>("REPORT_TOO_LARGE",
                $"This report has {rows.TotalRows:N0} rows; a download holds at most {limit:N0}. Choose a municipality or barangay to narrow it.");
        }

        var parameterLines = scope.Value.Lines(report.Parameters);
        var document = new ReportDocument(report.Title, await HeaderAsync(report, parameterLines, cancellationToken), report.Columns, rows.Rows,
            rows.Totals, rows.Notes);
        var what = $"{report.Title} ({string.Join("; ", parameterLines)}), {rows.TotalRows:N0} rows";
        await events.WriteExportAsync(new ExportEvent(AuditTrailService.ExportModule, AuditTable, Guid.Empty, Truncate(what, 200),
            format == ReportFormat.Csv ? ExportFormats.Csv : ExportFormats.Excel), cancellationToken);
        var fileName = $"{report.Code.ToLowerInvariant().Replace('_', '-')}-{scope.Value.AsOf:yyyyMMdd}.{writer.Extension}";
        return Result.Success(new ReportExport(fileName, writer.ContentType, (stream, ct) => writer.WriteAsync(document, stream, ct)));
    }

    private IReport? Find(string code) => reports.FirstOrDefault(r => string.Equals(r.Code, code, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Checks the parameters: a barangay of the named municipality, both in the user's jurisdiction. Without a municipality
    /// the report covers the user's whole jurisdiction (the database's filters, as on every screen).
    /// </summary>
    private async Task<Result<ReportScope>> ScopeAsync(IReport report, ReportRunRequest request, CancellationToken ct)
    {
        var asOf = request.AsOf ?? clock.Today;
        if (asOf == default || asOf > clock.Today)
        {
            return Result.Failure<ReportScope>("VALIDATION_FAILED", "The as-of date cannot be later than today.");
        }
        var municipalityId = report.Parameters.Contains(ReportParameter.Municipality) ? request.MunicipalityId : null;
        var barangayId = report.Parameters.Contains(ReportParameter.Barangay) ? request.BarangayId : null;
        string? barangayName = null;
        if (barangayId is { } b)
        {
            var barangay = await db.Barangays.Where(x => x.Id == b).Select(x => new { x.MunicipalityId, x.Name }).FirstOrDefaultAsync(ct);
            if (barangay is null)
            {
                return Result.Failure<ReportScope>("BARANGAY_NOT_FOUND", "The specified barangay does not exist.");
            }
            if (municipalityId is { } named && named != barangay.MunicipalityId)
            {
                return Result.Failure<ReportScope>("VALIDATION_FAILED", "The barangay is not in the named municipality.");
            }
            municipalityId = barangay.MunicipalityId;
            barangayName = barangay.Name;
        }
        string? municipalityName = null;
        if (municipalityId is { } m)
        {
            municipalityName = await db.Municipalities.Where(x => x.Id == m).Select(x => x.Name).FirstOrDefaultAsync(ct);
            if (municipalityName is null)
            {
                return Result.Failure<ReportScope>("MUNICIPALITY_NOT_FOUND", "The specified municipality does not exist.");
            }
            if (!jurisdiction.Allows(m))
            {
                return Result.Failure<ReportScope>(JurisdictionErrors.Code, JurisdictionErrors.Message);
            }
        }
        var scope = new ReportScope(asOf, municipalityId, barangayId, municipalityName, barangayName);
        if (report.Parameters.Contains(ReportParameter.Period))
        {
            // Without dates, the year to date; the period's end is the report's as-of date (and its file name's).
            var to = request.ToDate ?? clock.Today;
            var from = request.FromDate ?? new DateOnly(to.Year, 1, 1);
            if (to > clock.Today || from > to)
            {
                return Result.Failure<ReportScope>("VALIDATION_FAILED", "The period cannot start after it ends or end later than today.");
            }
            scope = scope with { AsOf = to, From = from, To = to };
        }
        foreach (var (parameter, months) in new[] { (ReportParameter.Month, 1), (ReportParameter.Quarter, 3), (ReportParameter.HalfYear, 6) })
        {
            if (!report.Parameters.Contains(parameter))
            {
                continue;
            }
            // Any day of the period names it; without one, the previous month or quarter, or the current half-year.
            var day = request.FromDate ?? (months == 6 ? clock.Today : clock.Today.AddMonths(-months));
            var start = new DateOnly(day.Year, (day.Month - 1) / months * months + 1, 1);
            if (start > clock.Today)
            {
                return Result.Failure<ReportScope>("VALIDATION_FAILED", "The period cannot start later than today.");
            }
            var end = ReportScope.PeriodEnd(start, months);
            var to = end < clock.Today ? end : clock.Today;
            scope = scope with { AsOf = to, From = start, To = to };
        }
        if (report.Parameters.Contains(ReportParameter.TdStatus) && request.Status is { } status)
        {
            if (!Enum.IsDefined(status))
            {
                return Result.Failure<ReportScope>("VALIDATION_FAILED", "The status is not a Tax Declaration status.");
            }
            scope = scope with { Status = status };
        }
        if (report.Parameters.Contains(ReportParameter.TransactionCode) && Trimmed(request.TransactionCode) is { } code)
        {
            if (code.Length > 20)
            {
                return Result.Failure<ReportScope>("VALIDATION_FAILED", "A transaction code has at most 20 characters.");
            }
            scope = scope with { TransactionCode = code.ToUpperInvariant() };
        }
        if (report.Parameters.Contains(ReportParameter.Pin) && Trimmed(request.Pin) is { } pin)
        {
            if (pin.Length > 100)
            {
                return Result.Failure<ReportScope>("VALIDATION_FAILED", "A PIN has at most 100 characters.");
            }
            scope = scope with { Pin = pin };
        }
        return Result.Success(scope);
    }

    private static string? Trimmed(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    /// <summary>The header block of a file (§4.1): LGU, office, title, parameters, when it was run and by whom.</summary>
    private Task<IReadOnlyList<string>> HeaderAsync(IReport report, IReadOnlyList<string> parameterLines, CancellationToken ct) =>
        ReportHeader.BuildAsync(db, office, currentUser, clock, lgu.Value, report.Title, parameterLines, ct);

    internal static string Truncate(string text, int length) => text.Length <= length ? text : text[..(length - 1)] + "…";
}
