using Microsoft.AspNetCore.Mvc;
using Prime.Application.Features.Offices;

namespace Prime.WebApi.Controllers;

/// <summary>
/// Offices, jurisdictions and user assignments (docs/analysis/province-wide-operation.md §3.1–§3.2).
/// Role gating of these administration endpoints is Phase 12 (decision Q12).
/// </summary>
[Route("api/offices")]
public class OfficesController(IOfficeService offices) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<OfficeDto>>> List(CancellationToken ct) => HandleResult(await offices.ListAsync(ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<OfficeDto>> Get(Guid id, CancellationToken ct) => HandleResult(await offices.GetAsync(id, ct));

    [HttpPost]
    public async Task<ActionResult<OfficeDto>> Create(CreateOfficeRequest request, CancellationToken ct) =>
        HandleCreated(await offices.CreateAsync(request, ct), nameof(Get), dto => new { id = dto.Id });

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<OfficeDto>> Update(Guid id, UpdateOfficeRequest request, CancellationToken ct) =>
        HandleResult(await offices.UpdateAsync(id, request, ct));

    [HttpGet("~/api/office-jurisdictions")]
    public async Task<ActionResult<IReadOnlyList<OfficeJurisdictionDto>>> ListJurisdictions([FromQuery] Guid? officeId, [FromQuery] Guid? municipalityId,
        CancellationToken ct) => HandleResult(await offices.ListJurisdictionsAsync(officeId, municipalityId, ct));

    [HttpPost("~/api/office-jurisdictions")]
    public async Task<ActionResult<OfficeJurisdictionDto>> CreateJurisdiction(CreateOfficeJurisdictionRequest request, CancellationToken ct) =>
        HandleResult(await offices.CreateJurisdictionAsync(request, ct));

    [HttpPost("~/api/office-jurisdictions/{id:guid}/approve")]
    public async Task<ActionResult<OfficeJurisdictionDto>> ApproveJurisdiction(Guid id, CancellationToken ct) =>
        HandleResult(await offices.ApproveJurisdictionAsync(id, ct));

    [HttpGet("~/api/office-assignments")]
    public async Task<ActionResult<IReadOnlyList<OfficeAssignmentDto>>> ListAssignments([FromQuery] Guid? officeId, [FromQuery] Guid? userId,
        CancellationToken ct) => HandleResult(await offices.ListAssignmentsAsync(officeId, userId, ct));

    [HttpPost("~/api/office-assignments")]
    public async Task<ActionResult<OfficeAssignmentDto>> CreateAssignment(CreateOfficeAssignmentRequest request, CancellationToken ct) =>
        HandleResult(await offices.CreateAssignmentAsync(request, ct));

    [HttpPost("~/api/office-assignments/{id:guid}/approve")]
    public async Task<ActionResult<OfficeAssignmentDto>> ApproveAssignment(Guid id, CancellationToken ct) =>
        HandleResult(await offices.ApproveAssignmentAsync(id, ct));

    [HttpPost("~/api/office-assignments/{id:guid}/end")]
    public async Task<ActionResult<OfficeAssignmentDto>> EndAssignment(Guid id, EndOfficeAssignmentRequest request, CancellationToken ct) =>
        HandleResult(await offices.EndAssignmentAsync(id, request, ct));

    [HttpGet("~/api/roles")]
    public async Task<ActionResult<IReadOnlyList<RoleDto>>> ListRoles(CancellationToken ct) => HandleResult(await offices.ListRolesAsync(ct));

    [HttpGet("~/api/users")]
    public async Task<ActionResult<IReadOnlyList<UserSummaryDto>>> ListUsers(CancellationToken ct) => HandleResult(await offices.ListUsersAsync(ct));

    /// <summary>The signed-in user, their office and what it covers.</summary>
    [HttpGet("~/api/me")]
    public async Task<ActionResult<CurrentUserDto>> Me(CancellationToken ct) => HandleResult(await offices.GetCurrentAsync(ct));
}
