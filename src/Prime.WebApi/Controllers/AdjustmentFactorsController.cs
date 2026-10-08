using Microsoft.AspNetCore.Mvc;
using Prime.Application.Features.Smv;
using Prime.Application.Common.Security;
using Prime.WebApi.Authorization;

namespace Prime.WebApi.Controllers;

/// <summary>SMV market value adjustment factors (docs/analysis/mrpaao-forms-model.md §8.3).</summary>
[Route("api/adjustment-factors")]
public class AdjustmentFactorsController(IAdjustmentFactorService factors) : ApiControllerBase
{
    [RequirePermission(Permissions.ConfigEdit)]
    [HttpPost]
    public async Task<ActionResult<AdjustmentFactorDto>> Create(CreateAdjustmentFactorRequest request, CancellationToken ct) =>
        HandleResult(await factors.CreateAsync(request, ct));

    [RequirePermission(Permissions.ConfigApprove)]
    [HttpPost("{id:guid}/approve")]
    public async Task<ActionResult<AdjustmentFactorDto>> Approve(Guid id, CancellationToken ct) =>
        HandleResult(await factors.ApproveAsync(id, ct));

    [RequirePermission(Permissions.PrimeUse)]
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AdjustmentFactorDto>>> List([FromQuery] Guid? smvId, CancellationToken ct) =>
        HandleResult(await factors.ListAsync(smvId, ct));
}
