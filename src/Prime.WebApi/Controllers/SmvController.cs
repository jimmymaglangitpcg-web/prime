using Microsoft.AspNetCore.Mvc;
using Prime.Application.Common;
using Prime.Application.Features.Smv;

namespace Prime.WebApi.Controllers;

// Explicit route: CLAUDE.md §62 specifies "/api/smv" (not the pluralized
// default [controller] token would produce).
[Route("api/smv")]
public class SmvController(ISmvService smvService) : ApiControllerBase
{
    [HttpPost]
    public async Task<ActionResult<SmvDto>> Create(CreateSmvRequest request, CancellationToken cancellationToken) =>
        HandleCreated(await smvService.CreateSmvAsync(request, cancellationToken), nameof(GetById), dto => new { id = dto.Id });

    [HttpGet]
    public async Task<ActionResult<PagedResult<SmvDto>>> List([FromQuery] PagedRequest request, CancellationToken cancellationToken) =>
        HandleResult(await smvService.ListAsync(request, cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SmvDto>> GetById(Guid id, CancellationToken cancellationToken) =>
        HandleResult(await smvService.GetByIdAsync(id, cancellationToken));

    [HttpPost("{id:guid}/approve")]
    public async Task<ActionResult<SmvDto>> Approve(Guid id, CancellationToken cancellationToken) =>
        HandleResult(await smvService.ApproveSmvAsync(id, cancellationToken));

    [HttpPost("{smvId:guid}/schedules")]
    public async Task<ActionResult<SmvScheduleDto>> CreateSchedule(Guid smvId, CreateSmvScheduleRequest request, CancellationToken cancellationToken) =>
        HandleCreated(await smvService.CreateScheduleAsync(smvId, request, cancellationToken), nameof(ListSchedules), dto => new { smvId = dto.SmvId });

    [HttpGet("{smvId:guid}/schedules")]
    public async Task<ActionResult<IReadOnlyList<SmvScheduleDto>>> ListSchedules(Guid smvId, CancellationToken cancellationToken) =>
        HandleResult(await smvService.ListSchedulesAsync(smvId, cancellationToken));

    [HttpPost("schedules/{scheduleId:guid}/approve")]
    public async Task<ActionResult<SmvScheduleDto>> ApproveSchedule(Guid scheduleId, CancellationToken cancellationToken) =>
        HandleResult(await smvService.ApproveScheduleAsync(scheduleId, cancellationToken));
}
