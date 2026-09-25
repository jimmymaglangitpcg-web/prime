using Microsoft.AspNetCore.Mvc;
using Prime.Application.Features.Smv;

namespace Prime.WebApi.Controllers;

/// <summary>SMV market value adjustment factors (docs/analysis/mrpaao-forms-model.md §8.3).</summary>
[Route("api/adjustment-factors")]
public class AdjustmentFactorsController(IAdjustmentFactorService factors) : ApiControllerBase
{
    [HttpPost]
    public async Task<ActionResult<AdjustmentFactorDto>> Create(CreateAdjustmentFactorRequest request, CancellationToken ct) =>
        HandleResult(await factors.CreateAsync(request, ct));

    [HttpPost("{id:guid}/approve")]
    public async Task<ActionResult<AdjustmentFactorDto>> Approve(Guid id, CancellationToken ct) =>
        HandleResult(await factors.ApproveAsync(id, ct));

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AdjustmentFactorDto>>> List([FromQuery] Guid? smvId, CancellationToken ct) =>
        HandleResult(await factors.ListAsync(smvId, ct));
}
