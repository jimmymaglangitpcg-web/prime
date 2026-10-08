using Microsoft.AspNetCore.Mvc;
using Prime.Application.Features.Assessments;
using Prime.Application.Common.Security;
using Prime.WebApi.Authorization;

namespace Prime.WebApi.Controllers;

/// <summary>Back taxes: one valuation and assessment per SMV period (docs/analysis/valuation-foundation.md §4.8).</summary>
[Route("api/back-taxes")]
public class BackTaxesController(IBackTaxService backTaxes) : ApiControllerBase
{
    [RequirePermission(Permissions.AssessmentPrepare)]
    [HttpPost("preview")]
    public async Task<ActionResult<BackTaxRunDto>> Preview(BackTaxRequest request, CancellationToken ct) =>
        HandleResult(await backTaxes.PreviewAsync(request, ct));

    [RequirePermission(Permissions.AssessmentPrepare)]
    [HttpPost]
    public async Task<ActionResult<BackTaxRunDto>> Create(BackTaxRequest request, CancellationToken ct) =>
        HandleResult(await backTaxes.CreateAsync(request, ct));

    [RequirePermission(Permissions.PropertyView)]
    [HttpGet("~/api/rpus/{rpuId:guid}/back-taxes")]
    public async Task<ActionResult<IReadOnlyList<BackTaxRunDto>>> ListByRpu(Guid rpuId, CancellationToken ct) =>
        HandleResult(await backTaxes.ListByRpuAsync(rpuId, ct));
}
