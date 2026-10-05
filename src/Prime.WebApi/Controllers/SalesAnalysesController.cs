using Microsoft.AspNetCore.Mvc;
using Prime.Application.Features.Smv;

namespace Prime.WebApi.Controllers;

/// <summary>Sales analyses and time-adjustment factors of an SMV preparation (docs/analysis/smv-preparation-general-revision.md §4.2).</summary>
[Route("api/smv")]
public class SalesAnalysesController(ISalesAnalysisService analyses, ISmvSubClassCriteriaService criteria) : ApiControllerBase
{
    /// <summary>The SMV's sub-class criteria (SMV Form 1).</summary>
    [HttpGet("{smvId:guid}/sub-class-criteria")]
    public async Task<ActionResult<IReadOnlyList<SmvSubClassCriterionDto>>> Criteria(Guid smvId, CancellationToken ct) =>
        HandleResult(await criteria.ListAsync(smvId, ct));

    [HttpPut("{smvId:guid}/sub-class-criteria")]
    public async Task<ActionResult<IReadOnlyList<SmvSubClassCriterionDto>>> SetCriteria(Guid smvId, SetSmvSubClassCriteriaRequest request, CancellationToken ct) =>
        HandleResult(await criteria.SetAsync(smvId, request, ct));

    [HttpGet("preparations/{preparationId:guid}/time-factors")]
    public async Task<ActionResult<IReadOnlyList<TimeAdjustmentFactorDto>>> TimeFactors(Guid preparationId, CancellationToken ct) =>
        HandleResult(await analyses.ListTimeFactorsAsync(preparationId, ct));

    [HttpPost("preparations/{preparationId:guid}/time-factors")]
    public async Task<ActionResult<IReadOnlyList<TimeAdjustmentFactorDto>>> AddTimeFactor(Guid preparationId, AddTimeAdjustmentFactorRequest request, CancellationToken ct) =>
        HandleResult(await analyses.AddTimeFactorAsync(preparationId, request, ct));

    [HttpDelete("preparations/{preparationId:guid}/time-factors/{factorId:guid}")]
    public async Task<ActionResult<IReadOnlyList<TimeAdjustmentFactorDto>>> RemoveTimeFactor(Guid preparationId, Guid factorId, CancellationToken ct) =>
        HandleResult(await analyses.RemoveTimeFactorAsync(preparationId, factorId, ct));

    [HttpGet("preparations/{preparationId:guid}/analyses")]
    public async Task<ActionResult<IReadOnlyList<SalesAnalysisSummaryDto>>> List(Guid preparationId, CancellationToken ct) =>
        HandleResult(await analyses.ListAsync(preparationId, ct));

    [HttpPost("preparations/{preparationId:guid}/analyses")]
    public async Task<ActionResult<SalesAnalysisDto>> Create(Guid preparationId, CreateSalesAnalysisRequest request, CancellationToken ct) =>
        HandleResult(await analyses.CreateAsync(preparationId, request, ct));

    [HttpGet("analyses/{id:guid}")]
    public async Task<ActionResult<SalesAnalysisDto>> Get(Guid id, CancellationToken ct) => HandleResult(await analyses.GetAsync(id, ct));

    [HttpPut("analyses/{id:guid}")]
    public async Task<ActionResult<SalesAnalysisDto>> Update(Guid id, UpdateSalesAnalysisRequest request, CancellationToken ct) =>
        HandleResult(await analyses.UpdateAsync(id, request, ct));

    [HttpPost("analyses/{id:guid}/refresh")]
    public async Task<ActionResult<SalesAnalysisDto>> Refresh(Guid id, CancellationToken ct) => HandleResult(await analyses.RefreshAsync(id, ct));

    [HttpPut("analyses/{id:guid}/sales/{saleId:guid}")]
    public async Task<ActionResult<SalesAnalysisDto>> UpdateSale(Guid id, Guid saleId, UpdateAnalysisSaleRequest request, CancellationToken ct) =>
        HandleResult(await analyses.UpdateSaleAsync(id, saleId, request, ct));

    [HttpPut("analyses/{id:guid}/groups")]
    public async Task<ActionResult<SalesAnalysisDto>> SetGroups(Guid id, SetSalesAnalysisGroupsRequest request, CancellationToken ct) =>
        HandleResult(await analyses.SetGroupsAsync(id, request, ct));

    [HttpPost("analyses/{id:guid}/groups/{groupId:guid}/adopt")]
    public async Task<ActionResult<SalesAnalysisDto>> Adopt(Guid id, Guid groupId, CancellationToken ct) =>
        HandleResult(await analyses.AdoptGroupAsync(id, groupId, ct));
}
