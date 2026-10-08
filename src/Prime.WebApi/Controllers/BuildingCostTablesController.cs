using Microsoft.AspNetCore.Mvc;
using Prime.Application.Features.Smv;
using Prime.Application.Common.Security;
using Prime.WebApi.Authorization;

namespace Prime.WebApi.Controllers;

/// <summary>The SMV's building tables: construction costs, extra-item costs, depreciation tables (docs/analysis/valuation-foundation.md §4.5).</summary>
[Route("api")]
public class BuildingCostTablesController(IBuildingCostTableService tables) : ApiControllerBase
{
    [RequirePermission(Permissions.ConfigEdit)]
    [HttpPost("building-costs")]
    public async Task<ActionResult<BuildingCostDto>> CreateBuildingCost(CreateBuildingCostRequest request, CancellationToken ct) =>
        HandleResult(await tables.CreateBuildingCostAsync(request, ct));

    [RequirePermission(Permissions.ConfigApprove)]
    [HttpPost("building-costs/{id:guid}/approve")]
    public async Task<ActionResult<BuildingCostDto>> ApproveBuildingCost(Guid id, CancellationToken ct) =>
        HandleResult(await tables.ApproveBuildingCostAsync(id, ct));

    [RequirePermission(Permissions.PrimeUse)]
    [HttpGet("building-costs")]
    public async Task<ActionResult<IReadOnlyList<BuildingCostDto>>> ListBuildingCosts([FromQuery] Guid? smvId, CancellationToken ct) =>
        HandleResult(await tables.ListBuildingCostsAsync(smvId, ct));

    [RequirePermission(Permissions.ConfigEdit)]
    [HttpPost("extra-item-costs")]
    public async Task<ActionResult<ExtraItemCostDto>> CreateExtraItemCost(CreateExtraItemCostRequest request, CancellationToken ct) =>
        HandleResult(await tables.CreateExtraItemCostAsync(request, ct));

    [RequirePermission(Permissions.ConfigApprove)]
    [HttpPost("extra-item-costs/{id:guid}/approve")]
    public async Task<ActionResult<ExtraItemCostDto>> ApproveExtraItemCost(Guid id, CancellationToken ct) =>
        HandleResult(await tables.ApproveExtraItemCostAsync(id, ct));

    [RequirePermission(Permissions.PrimeUse)]
    [HttpGet("extra-item-costs")]
    public async Task<ActionResult<IReadOnlyList<ExtraItemCostDto>>> ListExtraItemCosts([FromQuery] Guid? smvId, CancellationToken ct) =>
        HandleResult(await tables.ListExtraItemCostsAsync(smvId, ct));

    [RequirePermission(Permissions.ConfigEdit)]
    [HttpPost("depreciation-schedules")]
    public async Task<ActionResult<DepreciationScheduleDto>> CreateDepreciationSchedule(CreateDepreciationScheduleRequest request, CancellationToken ct) =>
        HandleResult(await tables.CreateDepreciationScheduleAsync(request, ct));

    [RequirePermission(Permissions.ConfigApprove)]
    [HttpPost("depreciation-schedules/{id:guid}/approve")]
    public async Task<ActionResult<DepreciationScheduleDto>> ApproveDepreciationSchedule(Guid id, CancellationToken ct) =>
        HandleResult(await tables.ApproveDepreciationScheduleAsync(id, ct));

    [RequirePermission(Permissions.PrimeUse)]
    [HttpGet("depreciation-schedules")]
    public async Task<ActionResult<IReadOnlyList<DepreciationScheduleDto>>> ListDepreciationSchedules([FromQuery] Guid? smvId, CancellationToken ct) =>
        HandleResult(await tables.ListDepreciationSchedulesAsync(smvId, ct));
}
