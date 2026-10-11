using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Net.Http.Headers;
using Prime.Application.Common;
using Prime.Application.Common.Security;
using Prime.Application.Features.Reports;
using Prime.WebApi.Authorization;
using Prime.WebApi.Security;

namespace Prime.WebApi.Controllers;

/// <summary>
/// Reports (CLAUDE.md §57; docs/analysis/reporting.md §4.1): the catalogue, a page on screen (records.view) and a download as
/// CSV or Excel (records.export, Q10). Downloads are audited as EXPORT by the report service.
/// </summary>
[Route("api/reports")]
public class ReportsController(IReportService reports, IRunExportService runs, IAuditExportService auditExports) : ApiControllerBase
{
    [RequirePermission(Permissions.RecordsView)]
    [HttpGet]
    public ActionResult<IReadOnlyList<ReportDefinitionDto>> List() => Ok(reports.List());

    [RequirePermission(Permissions.RecordsView)]
    [HttpPost("{code}/preview")]
    public async Task<ActionResult<ReportPreviewDto>> Preview(string code, ReportPreviewRequest request, CancellationToken ct) =>
        HandleResult(await reports.PreviewAsync(code, request, ct));

    [RequirePermission(Permissions.RecordsExport)]
    [HttpGet("{code}/export")]
    [EnableRateLimiting(RateLimiting.StrictPolicy)]
    public Task<ActionResult> Export(string code, [FromQuery] string? format, [FromQuery] ReportRunRequest request, CancellationToken ct) =>
        WriteAsync(format, chosen => reports.ExportAsync(code, request, chosen, ct), ct);

    /// <summary>A register run's rows as CSV or Excel (step R3): its issued snapshot, else read now.</summary>
    [RequirePermission(Permissions.RecordsExport)]
    [HttpGet("register-runs/{id:guid}/export")]
    [EnableRateLimiting(RateLimiting.StrictPolicy)]
    public Task<ActionResult> ExportRegisterRun(Guid id, [FromQuery] string? format, CancellationToken ct) =>
        WriteAsync(format, chosen => runs.RegisterRunAsync(id, chosen, ct), ct);

    /// <summary>A lowest-to-highest sales report run's groups as CSV or Excel (step R3).</summary>
    [RequirePermission(Permissions.RecordsExport)]
    [HttpGet("sales-report-runs/{id:guid}/export")]
    [EnableRateLimiting(RateLimiting.StrictPolicy)]
    public Task<ActionResult> ExportSalesReportRun(Guid id, [FromQuery] string? format, CancellationToken ct) =>
        WriteAsync(format, chosen => runs.SalesReportRunAsync(id, chosen, ct), ct);

    /// <summary>
    /// The audit viewer's filtered rows as CSV or Excel (step R6). The audit trail holds old and new values, so a download
    /// needs records.export as well as audit.view.
    /// </summary>
    [RequirePermission(Permissions.AuditView)]
    [RequirePermission(Permissions.RecordsExport)]
    [HttpGet("~/api/audit-logs/export")]
    [EnableRateLimiting(RateLimiting.StrictPolicy)]
    public Task<ActionResult> ExportAuditTrail([FromQuery] string? format, [FromQuery] Application.Features.Audit.AuditLogQuery query, CancellationToken ct) =>
        WriteAsync(format, chosen => auditExports.ExportAsync(query, chosen, ct), ct);

    private async Task<ActionResult> WriteAsync(string? format, Func<ReportFormat, Task<Result<ReportExport>>> export, CancellationToken ct)
    {
        var parsed = format?.ToLowerInvariant() switch
        {
            "csv" => ReportFormat.Csv,
            "xlsx" => ReportFormat.Xlsx,
            _ => (ReportFormat?)null,
        };
        if (parsed is not { } chosen)
        {
            return HandleResult(Result.Failure<object>("VALIDATION_FAILED", "The format must be csv or xlsx.")).Result!;
        }
        var result = await export(chosen);
        if (!result.IsSuccess)
        {
            return HandleResult(Result.Failure<object>(result.Code!, result.Message!)).Result!;
        }
        var file = result.Value;
        Response.ContentType = file.ContentType;
        Response.Headers[HeaderNames.ContentDisposition] = new ContentDispositionHeaderValue("attachment") { FileNameStar = file.FileName }.ToString();
        Response.Headers[HeaderNames.CacheControl] = "no-store";
        await file.WriteAsync(Response.Body, ct);
        return new EmptyResult();
    }
}
