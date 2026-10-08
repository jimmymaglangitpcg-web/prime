using Microsoft.AspNetCore.Mvc;
using Prime.Application.Features.Valuation;
using Prime.Application.Common.Security;
using Prime.WebApi.Authorization;

namespace Prime.WebApi.Controllers;

/// <summary>Independent appraisals: values determined outside the SMV (docs/analysis/valuation-foundation.md §4.7).</summary>
[Route("api/independent-appraisals")]
public class IndependentAppraisalsController(IIndependentAppraisalService appraisals) : ApiControllerBase
{
    [RequirePermission(Permissions.AppraisalPrepare)]
    [HttpPost]
    public async Task<ActionResult<IndependentAppraisalDto>> Create(CreateIndependentAppraisalRequest request, CancellationToken ct) =>
        HandleResult(await appraisals.CreateAsync(request, ct));

    [RequirePermission(Permissions.AppraisalPrepare)]
    [HttpPost("{id:guid}/withdraw")]
    public async Task<ActionResult<IndependentAppraisalDto>> Withdraw(Guid id, WithdrawIndependentAppraisalRequest request, CancellationToken ct) =>
        HandleResult(await appraisals.WithdrawAsync(id, request, ct));

    [RequirePermission(Permissions.PropertyView)]
    [HttpGet("~/api/rpus/{rpuId:guid}/independent-appraisals")]
    public async Task<ActionResult<IReadOnlyList<IndependentAppraisalDto>>> ListByRpu(Guid rpuId, CancellationToken ct) =>
        HandleResult(await appraisals.ListByRpuAsync(rpuId, ct));
}
