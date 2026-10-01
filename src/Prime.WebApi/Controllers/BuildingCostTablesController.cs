using Microsoft.AspNetCore.Mvc;
using Prime.Application.Features.Smv;

namespace Prime.WebApi.Controllers;

/// <summary>The SMV's building tables: construction costs, extra-item costs, depreciation tables (docs/analysis/valuation-foundation.md §4.5).</summary>
[Route("api")]
public class BuildingCostTablesController(IBuildingCostTableService tables) : ApiControllerBase
{
    [HttpPost("building-costs")]
    public async Task<ActionResult<BuildingCostDto>> CreateBuildingCost(CreateBuildingCostRequest request, CancellationToken ct) =>
        HandleResult(await tables.CreateBuildingCostAsync(request, ct));

    [HttpPost("building-costs/{id:guid}/approve")]
    public async Task<ActionResult<BuildingCostDto>> ApproveBuildingCost(Guid id, CancellationToken ct) =>
        HandleResult(await tables.ApproveBuildingCostAsync(id, ct));

    [HttpGet("building-costs")]
    public async Task<ActionResult<IReadOnlyList<BuildingCostDto>>> ListBuildingCosts([FromQuery] Guid? smvId, CancellationToken ct) =>
        HandleResult(await tables.ListBuildingCostsAsync(smvId, ct));

    [HttpPost("extra-item-costs")]
    public async Task<ActionResult<ExtraItemCostDto>> CreateExtraItemCost(CreateExtraItemCostRequest request, CancellationToken ct) =>
        HandleResult(await tables.CreateExtraItemCostAsync(request, ct));

    [HttpPost("extra-item-costs/{id:guid}/approve")]
    public async Task<ActionResult<ExtraItemCostDto>> ApproveExtraItemCost(Guid id, CancellationToken ct) =>
        HandleResult(await tables.ApproveExtraItemCostAsync(id, ct));

    [HttpGet("extra-item-costs")]
    public async Task<ActionResult<IReadOnlyList<ExtraItemCostDto>>> ListExtraItemCosts([FromQuery] Guid? smvId, CancellationToken ct) =>
        HandleResult(await tables.ListExtraItemCostsAsync(smvId, ct));

    [HttpPost("depreciation-schedules")]
    public async Task<ActionResult<DepreciationScheduleDto>> CreateDepreciationSchedule(CreateDepreciationScheduleRequest request, CancellationToken ct) =>
        HandleResult(await tables.CreateDepreciationScheduleAsync(request, ct));

    [HttpPost("depreciation-schedules/{id:guid}/approve")]
    public async Task<ActionResult<DepreciationScheduleDto>> ApproveDepreciationSchedule(Guid id, CancellationToken ct) =>
        HandleResult(await tables.ApproveDepreciationScheduleAsync(id, ct));

    [HttpGet("depreciation-schedules")]
    public async Task<ActionResult<IReadOnlyList<DepreciationScheduleDto>>> ListDepreciationSchedules([FromQuery] Guid? smvId, CancellationToken ct) =>
        HandleResult(await tables.ListDepreciationSchedulesAsync(smvId, ct));
}
