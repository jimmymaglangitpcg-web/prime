using Microsoft.AspNetCore.Mvc;
using Prime.Application.Common;
using Prime.Application.Features.SmvSimulations;
using Prime.Application.Common.Security;
using Prime.WebApi.Authorization;

namespace Prime.WebApi.Controllers;

/// <summary>Revenue compliance and tax impact studies (docs/analysis/smv-preparation-general-revision.md §4.5).</summary>
[Route("api/smv/impact-studies")]
public class RevenueImpactStudiesController(IRevenueImpactStudyService studies) : ApiControllerBase
{
    [RequirePermission(Permissions.SmvView)]
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<RevenueImpactStudySummaryDto>>> List(CancellationToken ct) => HandleResult(await studies.ListAsync(ct));

    [RequirePermission(Permissions.SmvPrepare)]
    [HttpPost]
    public async Task<ActionResult<RevenueImpactStudyDto>> Create(SaveRevenueImpactStudyRequest request, CancellationToken ct) =>
        HandleResult(await studies.CreateAsync(request, ct));

    [RequirePermission(Permissions.SmvView)]
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<RevenueImpactStudyDto>> Get(Guid id, CancellationToken ct) => HandleResult(await studies.GetAsync(id, ct));

    [RequirePermission(Permissions.SmvPrepare)]
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<RevenueImpactStudyDto>> Update(Guid id, SaveRevenueImpactStudyRequest request, CancellationToken ct) =>
        HandleResult(await studies.UpdateAsync(id, request, ct));

    [RequirePermission(Permissions.SmvView)]
    [HttpGet("{id:guid}/units")]
    public async Task<ActionResult<PagedResult<TaxImpactUnitDto>>> Units(Guid id, [FromQuery] TaxImpactUnitSearch request, CancellationToken ct) =>
        HandleResult(await studies.SearchUnitsAsync(id, request, ct));
}
