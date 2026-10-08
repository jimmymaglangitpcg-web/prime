using Microsoft.AspNetCore.Mvc;
using Prime.Application.Common.Security;
using Prime.Application.Features.Users;
using Prime.Domain.Enums;
using Prime.WebApi.Authorization;

namespace Prime.WebApi.Controllers;

/// <summary>
/// Sign-up requests, disabling and enabling users, and sign-in events (docs/analysis/workflow-security.md §4.2). A
/// pending user reaches only the signed-in-only actions here and <c>GET /api/me</c>.
/// </summary>
[Route("api/sign-up")]
public class UserAccountsController(IUserAccountService accounts) : ApiControllerBase
{
    /// <summary>The offices and roles an applicant may ask for.</summary>
    [SignedInOnly]
    [HttpGet("options")]
    public async Task<ActionResult<SignUpOptionsDto>> Options(CancellationToken ct) => HandleResult(await accounts.GetSignUpOptionsAsync(ct));

    /// <summary>The signed-in user's own sign-up requests, newest first.</summary>
    [SignedInOnly]
    [HttpGet("mine")]
    public async Task<ActionResult<IReadOnlyList<SignUpRequestDto>>> Mine(CancellationToken ct) => HandleResult(await accounts.ListMySignUpRequestsAsync(ct));

    [SignedInOnly]
    [HttpPost]
    public async Task<ActionResult<SignUpRequestDto>> Submit(SubmitSignUpRequest request, CancellationToken ct) =>
        HandleResult(await accounts.SubmitSignUpAsync(request, ct));

    [RequirePermission(Permissions.UsersSignUpDecide)]
    [HttpGet("~/api/sign-up-requests")]
    public async Task<ActionResult<IReadOnlyList<SignUpRequestDto>>> List([FromQuery] WorkflowStatus? status, CancellationToken ct) =>
        HandleResult(await accounts.ListSignUpRequestsAsync(status, ct));

    [RequirePermission(Permissions.UsersSignUpDecide)]
    [HttpPost("~/api/sign-up-requests/{id:guid}/approve")]
    public async Task<ActionResult<SignUpRequestDto>> Approve(Guid id, ApproveSignUpRequest request, CancellationToken ct) =>
        HandleResult(await accounts.ApproveSignUpAsync(id, request, ct));

    [RequirePermission(Permissions.UsersSignUpDecide)]
    [HttpPost("~/api/sign-up-requests/{id:guid}/reject")]
    public async Task<ActionResult<SignUpRequestDto>> Reject(Guid id, DecisionReasonRequest request, CancellationToken ct) =>
        HandleResult(await accounts.RejectSignUpAsync(id, request, ct));

    [RequirePermission(Permissions.OfficeView)]
    [HttpGet("~/api/user-status-changes")]
    public async Task<ActionResult<IReadOnlyList<UserStatusChangeDto>>> ListStatusChanges([FromQuery] Guid? userId, CancellationToken ct) =>
        HandleResult(await accounts.ListStatusChangesAsync(userId, ct));

    /// <summary>Proposes disabling (Inactive) or enabling (Active) a user; a second user approves it.</summary>
    [RequirePermission(Permissions.UsersManage)]
    [HttpPost("~/api/users/{id:guid}/status-changes")]
    public async Task<ActionResult<UserStatusChangeDto>> ProposeStatusChange(Guid id, ProposeUserStatusRequest request, CancellationToken ct) =>
        HandleResult(await accounts.ProposeStatusChangeAsync(id, request, ct));

    [RequirePermission(Permissions.UsersApprove)]
    [HttpPost("~/api/user-status-changes/{id:guid}/approve")]
    public async Task<ActionResult<UserStatusChangeDto>> ApproveStatusChange(Guid id, CancellationToken ct) =>
        HandleResult(await accounts.ApproveStatusChangeAsync(id, ct));

    [RequirePermission(Permissions.UsersApprove)]
    [HttpPost("~/api/user-status-changes/{id:guid}/reject")]
    public async Task<ActionResult<UserStatusChangeDto>> RejectStatusChange(Guid id, DecisionReasonRequest request, CancellationToken ct) =>
        HandleResult(await accounts.RejectStatusChangeAsync(id, request, ct));

    /// <summary>The browser reports a sign-in or sign-out; logged as LOGIN or LOGOUT (CLAUDE.md §48).</summary>
    [SignedInOnly]
    [HttpPost("~/api/session")]
    public async Task<ActionResult<bool>> Session(SessionEventRequest request, CancellationToken ct) =>
        HandleResult(await accounts.RecordSessionEventAsync(request, ct));
}
