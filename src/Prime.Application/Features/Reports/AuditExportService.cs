using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Prime.Application.Common;
using Prime.Application.Common.Interfaces;
using Prime.Application.Features.Audit;
using Prime.Application.Features.Offices;

namespace Prime.Application.Features.Reports;

public interface IAuditExportService
{
    Task<Result<ReportExport>> ExportAsync(AuditLogQuery query, ReportFormat format, CancellationToken cancellationToken = default);
}

/// <summary>
/// The audit viewer's filtered rows as CSV or Excel (CLAUDE.md §57 Audit; docs/analysis/reporting.md §4.2, step R6): user
/// activity, record changes, approval and export history are the viewer's filters. Up to the download limit (Q3; larger
/// runs wait for R5). The file carries old and new values, so the endpoint needs <c>records.export</c> as well as
/// <c>audit.view</c>; each download writes an EXPORT row with the filter.
/// </summary>
public sealed class AuditExportService(
    IAuditTrailService audit,
    IEnumerable<IReportFileWriter> writers,
    IApplicationDbContext db,
    IClock clock,
    IOfficeContext office,
    ICurrentUserService currentUser,
    ISecurityEventLog events,
    IOptions<LguOptions> lgu,
    IOptions<ReportsOptions> options) : IAuditExportService
{
    private const string Title = "Audit trail";

    private static readonly IReadOnlyList<ReportColumn> Columns =
    [
        new("timestamp", "Date and time", ReportColumnType.Text),
        new("user", "User", ReportColumnType.Text),
        new("action", "Action", ReportColumnType.Text),
        new("module", "Module", ReportColumnType.Text),
        new("table", "Table", ReportColumnType.Text),
        new("recordId", "Record", ReportColumnType.Text),
        new("parentTable", "Parent table", ReportColumnType.Text),
        new("parentRecordId", "Parent record", ReportColumnType.Text),
        new("reason", "Reason", ReportColumnType.Text),
        new("ipAddress", "IP address", ReportColumnType.Text),
        new("oldValue", "Old value", ReportColumnType.Text),
        new("newValue", "New value", ReportColumnType.Text),
    ];

    public async Task<Result<ReportExport>> ExportAsync(AuditLogQuery query, ReportFormat format, CancellationToken cancellationToken = default)
    {
        if (writers.FirstOrDefault(w => w.Format == format) is not { } writer)
        {
            return Result.Failure<ReportExport>("VALIDATION_FAILED", "The format must be csv or xlsx.");
        }
        var rows = await audit.QueryAsync(query, cancellationToken);
        if (!rows.IsSuccess)
        {
            return Result.Failure<ReportExport>(rows.Code!, rows.Message!);
        }
        var limit = options.Value.SyncRowLimit;
        // One more than the limit tells an oversized filter apart without counting the whole trail.
        var read = await rows.Value.Take(limit + 1).ToListAsync(cancellationToken);
        if (read.Count > limit)
        {
            return Result.Failure<ReportExport>("REPORT_TOO_LARGE",
                $"The filter selects more than {limit:N0} audit rows; a download holds at most {limit:N0}. Narrow the period, user, table or action.");
        }

        var filter = Describe(query);
        var header = await ReportHeader.BuildAsync(db, office, currentUser, clock, lgu.Value, Title, filter, cancellationToken);
        var cells = read.Select(r => new object?[]
        {
            Local(r.Timestamp), r.UserName ?? (r.UserId is null ? "system" : r.UserId.ToString()), r.Action.ToString(), r.Module, r.TableName,
            r.RecordId.ToString(), r.ParentTableName, r.ParentRecordId?.ToString(), r.Reason, r.IpAddress, r.OldValue, r.NewValue,
        }).ToList();
        var notes = new List<string> { "Times are in the LGU's time zone. Old and new values are as the audit trail recorded them." };
        var document = new ReportDocument(Title, header, Columns, cells, null, notes);

        var what = ReportService.Truncate($"{Title} ({string.Join("; ", filter)}), {cells.Count:N0} rows", 200);
        await events.WriteExportAsync(new ExportEvent(AuditTrailService.ExportModule, "AuditLogs", Guid.Empty, what,
            format == ReportFormat.Csv ? ExportFormats.Csv : ExportFormats.Excel), cancellationToken);
        var fileName = $"audit-trail-{clock.Today:yyyyMMdd}.{writer.Extension}";
        return Result.Success(new ReportExport(fileName, writer.ContentType, (stream, ct) => writer.WriteAsync(document, stream, ct)));
    }

    /// <summary>An instant in the LGU's time zone, to the second.</summary>
    private string Local(DateTimeOffset instant) =>
        instant.ToOffset(clock.StartOfDay(clock.LocalDate(instant)).Offset).ToString("yyyy-MM-dd HH:mm:ss");

    /// <summary>The filter as the header block and the EXPORT row print it.</summary>
    private List<string> Describe(AuditLogQuery q)
    {
        var lines = new List<string>();
        void Add(string label, object? value)
        {
            if (value is not null && value.ToString() is { Length: > 0 } text)
            {
                lines.Add($"{label}: {text}");
            }
        }
        Add("User", q.UserId);
        Add("Table", q.TableName?.Trim());
        Add("Record", q.RecordId);
        Add("Property", q.PropertyId);
        Add("Action", q.Action);
        Add("Module", q.Module?.Trim());
        Add("From", q.From is { } f ? Local(f) : null);
        Add("Before", q.To is { } t ? Local(t) : null);
        if (lines.Count == 0)
        {
            lines.Add("Filter: none (the whole trail)");
        }
        return lines;
    }
}
