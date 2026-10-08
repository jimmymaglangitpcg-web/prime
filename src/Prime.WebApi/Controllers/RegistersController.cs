using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Prime.Application.Features.Registers;
using Prime.Application.Common.Security;
using Prime.WebApi.Auditing;
using Prime.WebApi.Authorization;
using Prime.WebApi.Security;

namespace Prime.WebApi.Controllers;

/// <summary>Dated runs of the MRPAAO registers (docs/analysis/mrpaao-forms-model.md §15); printed through the forms.</summary>
[Route("api/registers")]
public class RegistersController(IRegisterService registers) : ApiControllerBase
{
    [RequirePermission(Permissions.RecordsRun)]
    [HttpPost]
    [AuditExport("RegisterRuns", "Register run")]
    [EnableRateLimiting(RateLimiting.StrictPolicy)]
    public async Task<ActionResult<RegisterRunDto>> Create(CreateRegisterRunRequest request, CancellationToken ct) =>
        HandleResult(await registers.CreateRunAsync(request, ct));

    [RequirePermission(Permissions.RecordsView)]
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<RegisterRunDto>>> List(CancellationToken ct) => HandleResult(await registers.ListRunsAsync(ct));
}
