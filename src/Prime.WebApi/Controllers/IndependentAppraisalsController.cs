using Microsoft.AspNetCore.Mvc;
using Prime.Application.Features.Valuation;

namespace Prime.WebApi.Controllers;

/// <summary>Independent appraisals: values determined outside the SMV (docs/analysis/valuation-foundation.md §4.7).</summary>
[Route("api/independent-appraisals")]
public class IndependentAppraisalsController(IIndependentAppraisalService appraisals) : ApiControllerBase
{
    [HttpPost]
    public async Task<ActionResult<IndependentAppraisalDto>> Create(CreateIndependentAppraisalRequest request, CancellationToken ct) =>
        HandleResult(await appraisals.CreateAsync(request, ct));

    [HttpPost("{id:guid}/withdraw")]
    public async Task<ActionResult<IndependentAppraisalDto>> Withdraw(Guid id, WithdrawIndependentAppraisalRequest request, CancellationToken ct) =>
        HandleResult(await appraisals.WithdrawAsync(id, request, ct));

    [HttpGet("~/api/rpus/{rpuId:guid}/independent-appraisals")]
    public async Task<ActionResult<IReadOnlyList<IndependentAppraisalDto>>> ListByRpu(Guid rpuId, CancellationToken ct) =>
        HandleResult(await appraisals.ListByRpuAsync(rpuId, ct));
}
