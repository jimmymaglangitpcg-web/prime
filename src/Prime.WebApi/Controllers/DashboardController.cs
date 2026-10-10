using Microsoft.AspNetCore.Mvc;
using Prime.Application.Common.Security;
using Prime.Application.Features.Dashboard;
using Prime.WebApi.Authorization;

namespace Prime.WebApi.Controllers;

/// <summary>The dashboard (CLAUDE.md §55; docs/analysis/reporting.md §4.3): the user's jurisdiction as of today (prime.use, Q10).</summary>
[Route("api/dashboard")]
public class DashboardController(IDashboardService dashboard) : ApiControllerBase
{
    [RequirePermission(Permissions.PrimeUse)]
    [HttpGet]
    public async Task<ActionResult<DashboardDto>> Get(CancellationToken ct) => Ok(await dashboard.GetAsync(ct));
}
