using Microsoft.AspNetCore.Mvc;
using Prime.Application.Common;
using Prime.Application.Common.Security;
using Prime.Application.Features.Audit;
using Prime.WebApi.Authorization;

namespace Prime.WebApi.Controllers;

/// <summary>
/// The audit viewer and record histories, read-only (CLAUDE.md §48; docs/analysis/workflow-security.md §4.3). No
/// endpoint changes or deletes an audit row.
/// </summary>
[Route("api/audit-logs")]
public class AuditLogsController(IAuditTrailService audit) : ApiControllerBase
{
    [RequirePermission(Permissions.AuditView)]
    [HttpGet]
    public async Task<ActionResult<PagedResult<AuditLogDto>>> List([FromQuery] AuditLogQuery query, CancellationToken ct) =>
        HandleResult(await audit.ListAsync(query, ct));

    [RequirePermission(Permissions.AuditView)]
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AuditLogDto>> Get(Guid id, CancellationToken ct) => HandleResult(await audit.GetAsync(id, ct));

    /// <summary>The tables that have audit rows, for the viewer's filter.</summary>
    [RequirePermission(Permissions.AuditView)]
    [HttpGet("tables")]
    public async Task<ActionResult<IReadOnlyList<string>>> Tables(CancellationToken ct) => HandleResult(await audit.ListTablesAsync(ct));

    /// <summary>A print or download made in the browser, reported by the page that made it (an EXPORT row).</summary>
    [RequirePermission(Permissions.PrimeUse)]
    [HttpPost("~/api/audit/exports")]
    public async Task<ActionResult<bool>> RecordExport(RecordExportRequest request, CancellationToken ct) =>
        HandleResult(await audit.RecordExportAsync(request, ct));
}
