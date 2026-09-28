using Microsoft.AspNetCore.Mvc;
using Prime.Application.Features.Offices;

namespace Prime.WebApi.Controllers;

/// <summary>Delegations of final approval to municipal assessors (docs/analysis/province-wide-operation.md §3.4).</summary>
[Route("api/approval-delegations")]
public class ApprovalDelegationsController(IApprovalDelegationService delegations) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ApprovalDelegationDto>>> List([FromQuery] Guid? officeId, CancellationToken ct) =>
        HandleResult(await delegations.ListAsync(officeId, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApprovalDelegationDto>> Get(Guid id, CancellationToken ct) => HandleResult(await delegations.GetAsync(id, ct));

    [HttpPost]
    public async Task<ActionResult<ApprovalDelegationDto>> Create(CreateApprovalDelegationRequest request, CancellationToken ct) =>
        HandleCreated(await delegations.CreateAsync(request, ct), nameof(Get), dto => new { id = dto.Id });

    [HttpPost("{id:guid}/approve")]
    public async Task<ActionResult<ApprovalDelegationDto>> Approve(Guid id, CancellationToken ct) => HandleResult(await delegations.ApproveAsync(id, ct));

    [HttpPost("{id:guid}/reject")]
    public async Task<ActionResult<ApprovalDelegationDto>> Reject(Guid id, RejectApprovalDelegationRequest request, CancellationToken ct) =>
        HandleResult(await delegations.RejectAsync(id, request, ct));

    [HttpPost("{id:guid}/revoke")]
    public async Task<ActionResult<ApprovalDelegationDto>> Revoke(Guid id, RevokeApprovalDelegationRequest request, CancellationToken ct) =>
        HandleResult(await delegations.RevokeAsync(id, request, ct));
}
