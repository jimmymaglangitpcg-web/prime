using Microsoft.AspNetCore.Mvc;
using Prime.Application.Features.Transactions;
using Prime.Application.Common.Security;
using Prime.WebApi.Authorization;

namespace Prime.WebApi.Controllers;

/// <summary>Property transactions and their catalogue (CLAUDE.md §34–§37; docs/FORMS-REVISION-PLAN.md A5). Role gating is Phase 12.</summary>
[Route("api/transactions")]
public class TransactionsController(ITransactionService transactions) : ApiControllerBase
{
    [RequirePermission(Permissions.PrimeUse)]
    [HttpGet("types")]
    public async Task<ActionResult<IReadOnlyList<TransactionTypeDto>>> ListTypes([FromQuery] bool inForceOnly, CancellationToken ct) =>
        HandleResult(await transactions.ListTypesAsync(inForceOnly, ct));

    [RequirePermission(Permissions.ConfigEdit)]
    [HttpPost("types")]
    public async Task<ActionResult<TransactionTypeDto>> CreateType(CreateTransactionTypeRequest request, CancellationToken ct) =>
        HandleResult(await transactions.CreateTypeAsync(request, ct));

    [RequirePermission(Permissions.ConfigApprove)]
    [HttpPost("types/{id:guid}/approve")]
    public async Task<ActionResult<TransactionTypeDto>> ApproveType(Guid id, CancellationToken ct) =>
        HandleResult(await transactions.ApproveTypeAsync(id, ct));

    [RequirePermission(Permissions.TransactionPrepare)]
    [HttpPost]
    public async Task<ActionResult<PropertyTransactionDto>> Open(OpenTransactionRequest request, CancellationToken ct) =>
        HandleCreated(await transactions.OpenAsync(request, ct), nameof(Get), dto => new { id = dto.Id });

    [RequirePermission(Permissions.PropertyView)]
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PropertyTransactionDto>> Get(Guid id, CancellationToken ct) => HandleResult(await transactions.GetAsync(id, ct));

    [RequirePermission(Permissions.PropertyView)]
    [HttpGet("~/api/properties/{propertyId:guid}/transactions")]
    public async Task<ActionResult<IReadOnlyList<PropertyTransactionDto>>> ListByProperty(Guid propertyId, CancellationToken ct) =>
        HandleResult(await transactions.ListByPropertyAsync(propertyId, ct));

    [RequirePermission(Permissions.TransactionPrepare)]
    [HttpPost("{id:guid}/requirements/{requirementId:guid}/satisfy")]
    public async Task<ActionResult<PropertyTransactionDto>> Satisfy(Guid id, Guid requirementId, SatisfyRequirementRequest request, CancellationToken ct) =>
        HandleResult(await transactions.SatisfyRequirementAsync(id, requirementId, request, ct));

    [RequirePermission(Permissions.TransactionPrepare)]
    [HttpPost("{id:guid}/submit")]
    public async Task<ActionResult<PropertyTransactionDto>> Submit(Guid id, CancellationToken ct) => HandleResult(await transactions.SubmitAsync(id, ct));

    [RequirePermission(Permissions.TransactionApprove)]
    [HttpPost("{id:guid}/approve")]
    public async Task<ActionResult<PropertyTransactionDto>> Approve(Guid id, CancellationToken ct) => HandleResult(await transactions.ApproveAsync(id, ct));

    [RequirePermission(Permissions.TransactionApprove)]
    [HttpPost("{id:guid}/reject")]
    public async Task<ActionResult<PropertyTransactionDto>> Reject(Guid id, TransactionReasonRequest request, CancellationToken ct) =>
        HandleResult(await transactions.RejectAsync(id, request.Reason, ct));

    [RequirePermission(Permissions.TransactionPrepare)]
    [HttpPost("{id:guid}/withdraw")]
    public async Task<ActionResult<PropertyTransactionDto>> Withdraw(Guid id, TransactionReasonRequest request, CancellationToken ct) =>
        HandleResult(await transactions.WithdrawAsync(id, request.Reason, ct));
}
