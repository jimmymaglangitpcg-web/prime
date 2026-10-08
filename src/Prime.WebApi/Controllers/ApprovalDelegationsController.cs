using Microsoft.AspNetCore.Mvc;
using Prime.Application.Features.Offices;
using Prime.Application.Common.Security;
using Prime.WebApi.Authorization;

namespace Prime.WebApi.Controllers;

/// <summary>Delegations of final approval to municipal assessors (docs/analysis/province-wide-operation.md §3.4).</summary>
[Route("api/approval-delegations")]
public class ApprovalDelegationsController(IApprovalDelegationService delegations) : ApiControllerBase
{
    [RequirePermission(Permissions.OfficeView)]
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ApprovalDelegationDto>>> List([FromQuery] Guid? officeId, CancellationToken ct) =>
        HandleResult(await delegations.ListAsync(officeId, ct));

    [RequirePermission(Permissions.OfficeView)]
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApprovalDelegationDto>> Get(Guid id, CancellationToken ct) => HandleResult(await delegations.GetAsync(id, ct));

    [RequirePermission(Permissions.UsersManage)]
    [HttpPost]
    public async Task<ActionResult<ApprovalDelegationDto>> Create(CreateApprovalDelegationRequest request, CancellationToken ct) =>
        HandleCreated(await delegations.CreateAsync(request, ct), nameof(Get), dto => new { id = dto.Id });

    [RequirePermission(Permissions.UsersApprove)]
    [HttpPost("{id:guid}/approve")]
    public async Task<ActionResult<ApprovalDelegationDto>> Approve(Guid id, CancellationToken ct) => HandleResult(await delegations.ApproveAsync(id, ct));

    [RequirePermission(Permissions.UsersApprove)]
    [HttpPost("{id:guid}/reject")]
    public async Task<ActionResult<ApprovalDelegationDto>> Reject(Guid id, RejectApprovalDelegationRequest request, CancellationToken ct) =>
        HandleResult(await delegations.RejectAsync(id, request, ct));

    [RequirePermission(Permissions.UsersManage)]
    [HttpPost("{id:guid}/revoke")]
    public async Task<ActionResult<ApprovalDelegationDto>> Revoke(Guid id, RevokeApprovalDelegationRequest request, CancellationToken ct) =>
        HandleResult(await delegations.RevokeAsync(id, request, ct));
}
