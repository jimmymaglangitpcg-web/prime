using Microsoft.AspNetCore.Mvc;
using Prime.Application.Features.Offices;
using Prime.Application.Common.Security;
using Prime.WebApi.Authorization;

namespace Prime.WebApi.Controllers;

/// <summary>
/// Offices, jurisdictions and user assignments (docs/analysis/province-wide-operation.md §3.1–§3.2).
/// Role gating of these administration endpoints is Phase 12 (decision Q12).
/// </summary>
[Route("api/offices")]
public class OfficesController(IOfficeService offices) : ApiControllerBase
{
    [RequirePermission(Permissions.OfficeView)]
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<OfficeDto>>> List(CancellationToken ct) => HandleResult(await offices.ListAsync(ct));

    [RequirePermission(Permissions.OfficeView)]
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<OfficeDto>> Get(Guid id, CancellationToken ct) => HandleResult(await offices.GetAsync(id, ct));

    [RequirePermission(Permissions.UsersManage)]
    [HttpPost]
    public async Task<ActionResult<OfficeDto>> Create(CreateOfficeRequest request, CancellationToken ct) =>
        HandleCreated(await offices.CreateAsync(request, ct), nameof(Get), dto => new { id = dto.Id });

    [RequirePermission(Permissions.UsersManage)]
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<OfficeDto>> Update(Guid id, UpdateOfficeRequest request, CancellationToken ct) =>
        HandleResult(await offices.UpdateAsync(id, request, ct));

    [RequirePermission(Permissions.OfficeView)]
    [HttpGet("~/api/office-jurisdictions")]
    public async Task<ActionResult<IReadOnlyList<OfficeJurisdictionDto>>> ListJurisdictions([FromQuery] Guid? officeId, [FromQuery] Guid? municipalityId,
        CancellationToken ct) => HandleResult(await offices.ListJurisdictionsAsync(officeId, municipalityId, ct));

    [RequirePermission(Permissions.UsersManage)]
    [HttpPost("~/api/office-jurisdictions")]
    public async Task<ActionResult<OfficeJurisdictionDto>> CreateJurisdiction(CreateOfficeJurisdictionRequest request, CancellationToken ct) =>
        HandleResult(await offices.CreateJurisdictionAsync(request, ct));

    [RequirePermission(Permissions.UsersApprove)]
    [HttpPost("~/api/office-jurisdictions/{id:guid}/approve")]
    public async Task<ActionResult<OfficeJurisdictionDto>> ApproveJurisdiction(Guid id, CancellationToken ct) =>
        HandleResult(await offices.ApproveJurisdictionAsync(id, ct));

    [RequirePermission(Permissions.OfficeView)]
    [HttpGet("~/api/office-assignments")]
    public async Task<ActionResult<IReadOnlyList<OfficeAssignmentDto>>> ListAssignments([FromQuery] Guid? officeId, [FromQuery] Guid? userId,
        CancellationToken ct) => HandleResult(await offices.ListAssignmentsAsync(officeId, userId, ct));

    [RequirePermission(Permissions.UsersManage)]
    [HttpPost("~/api/office-assignments")]
    public async Task<ActionResult<OfficeAssignmentDto>> CreateAssignment(CreateOfficeAssignmentRequest request, CancellationToken ct) =>
        HandleResult(await offices.CreateAssignmentAsync(request, ct));

    [RequirePermission(Permissions.UsersApprove)]
    [HttpPost("~/api/office-assignments/{id:guid}/approve")]
    public async Task<ActionResult<OfficeAssignmentDto>> ApproveAssignment(Guid id, CancellationToken ct) =>
        HandleResult(await offices.ApproveAssignmentAsync(id, ct));

    [RequirePermission(Permissions.UsersManage)]
    [HttpPost("~/api/office-assignments/{id:guid}/end")]
    public async Task<ActionResult<OfficeAssignmentDto>> EndAssignment(Guid id, EndOfficeAssignmentRequest request, CancellationToken ct) =>
        HandleResult(await offices.EndAssignmentAsync(id, request, ct));

    [RequirePermission(Permissions.OfficeView)]
    [HttpGet("~/api/roles")]
    public async Task<ActionResult<IReadOnlyList<RoleDto>>> ListRoles(CancellationToken ct) => HandleResult(await offices.ListRolesAsync(ct));

    [RequirePermission(Permissions.OfficeView)]
    [HttpGet("~/api/users")]
    public async Task<ActionResult<IReadOnlyList<UserSummaryDto>>> ListUsers(CancellationToken ct) => HandleResult(await offices.ListUsersAsync(ct));

    [RequirePermission(Permissions.UsersManage)]
    [HttpPut("~/api/users/{id:guid}/licence")]
    public async Task<ActionResult<UserSummaryDto>> UpdateUserLicence(Guid id, UpdateUserLicenceRequest request, CancellationToken ct) =>
        HandleResult(await offices.UpdateUserLicenceAsync(id, request, ct));

    /// <summary>The signed-in user, their office and what it covers.</summary>
    [SignedInOnly]
    [HttpGet("~/api/me")]
    public async Task<ActionResult<CurrentUserDto>> Me(CancellationToken ct) => HandleResult(await offices.GetCurrentAsync(ct));
}
