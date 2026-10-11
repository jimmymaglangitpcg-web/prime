using Microsoft.AspNetCore.Mvc;
using Prime.Application.Common.Security;
using Prime.Application.Features.LevyRates;
using Prime.WebApi.Authorization;

namespace Prime.WebApi.Controllers;

/// <summary>Levy rates for the report collectibles (docs/analysis/reporting.md §10, Q18): configuration under maker-checker.</summary>
[Route("api/levy-rates")]
public class LevyRatesController(ILevyRateService rates) : ApiControllerBase
{
    [RequirePermission(Permissions.PrimeUse)]
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<LevyRateDto>>> List([FromQuery] bool inForceOnly, CancellationToken ct) =>
        HandleResult(await rates.ListAsync(inForceOnly, ct));

    [RequirePermission(Permissions.ConfigEdit)]
    [HttpPost]
    public async Task<ActionResult<LevyRateDto>> Create(CreateLevyRateRequest request, CancellationToken ct) =>
        HandleResult(await rates.CreateAsync(request, ct));

    [RequirePermission(Permissions.ConfigApprove)]
    [HttpPost("{id:guid}/approve")]
    public async Task<ActionResult<LevyRateDto>> Approve(Guid id, CancellationToken ct) =>
        HandleResult(await rates.ApproveAsync(id, ct));
}
