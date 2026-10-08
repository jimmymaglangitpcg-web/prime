using Microsoft.AspNetCore.Mvc;
using Prime.Application.Common.Security;
using Prime.Application.Features.Security;
using Prime.WebApi.Authorization;

namespace Prime.WebApi.Controllers;

/// <summary>The role–permission matrix and its changes (docs/analysis/workflow-security.md §4.1, Q2).</summary>
[Route("api/role-permissions")]
public class RolePermissionsController(IRolePermissionService service) : ApiControllerBase
{
    [RequirePermission(Permissions.OfficeView)]
    [HttpGet]
    public async Task<ActionResult<PermissionMatrixDto>> Matrix(CancellationToken ct) => HandleResult(await service.GetMatrixAsync(ct));

    /// <summary>Proposes a role's new set of permissions; it applies when a second user approves it.</summary>
    [RequirePermission(Permissions.UsersManage)]
    [HttpPost("changes")]
    public async Task<ActionResult<RolePermissionChangeDto>> Propose(ProposeRolePermissionsRequest request, CancellationToken ct) =>
        HandleResult(await service.ProposeAsync(request, ct));

    [RequirePermission(Permissions.UsersApprove)]
    [HttpPost("changes/{id:guid}/approve")]
    public async Task<ActionResult<RolePermissionChangeDto>> Approve(Guid id, CancellationToken ct) => HandleResult(await service.ApproveAsync(id, ct));

    [RequirePermission(Permissions.UsersApprove)]
    [HttpPost("changes/{id:guid}/reject")]
    public async Task<ActionResult<RolePermissionChangeDto>> Reject(Guid id, DecideRolePermissionChangeRequest request, CancellationToken ct) =>
        HandleResult(await service.RejectAsync(id, request, ct));
}
