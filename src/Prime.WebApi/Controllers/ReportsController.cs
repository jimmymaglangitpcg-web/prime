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
public class ReportsController(IReportService reports) : ApiControllerBase
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
    public async Task<ActionResult> Export(string code, [FromQuery] string? format, [FromQuery] ReportRunRequest request, CancellationToken ct)
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
        var result = await reports.ExportAsync(code, request, chosen, ct);
        if (!result.IsSuccess)
        {
            return HandleResult(Result.Failure<object>(result.Code!, result.Message!)).Result!;
        }
        var export = result.Value;
        Response.ContentType = export.ContentType;
        Response.Headers[HeaderNames.ContentDisposition] = new ContentDispositionHeaderValue("attachment") { FileNameStar = export.FileName }.ToString();
        Response.Headers[HeaderNames.CacheControl] = "no-store";
        await export.WriteAsync(Response.Body, ct);
        return new EmptyResult();
    }
}
