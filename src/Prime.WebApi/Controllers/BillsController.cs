using Microsoft.AspNetCore.Mvc;
using Prime.Application.Features.Billing.Bills;
using Prime.Application.Common.Security;
using Prime.WebApi.Authorization;

namespace Prime.WebApi.Controllers;

/// <summary>
/// Tax bills (docs/BILLING.md §4, §6.1): generate (Draft) → post → cancel,
/// plus the per-property list and statement of account. Role gating
/// (Treasurer) is Phase 12.
/// </summary>
[Route("api/bills")]
public class BillsController(IBillService bills) : ApiControllerBase
{
    [RequirePermission(Permissions.TreasuryLegacy)]
    [HttpPost]
    public async Task<ActionResult<TaxBillDto>> Generate(GenerateBillRequest request, CancellationToken ct) =>
        HandleCreated(await bills.GenerateAsync(request, ct), nameof(GetById), dto => new { id = dto.Id });

    [RequirePermission(Permissions.TreasuryLegacy)]
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TaxBillDto>> GetById(Guid id, CancellationToken ct) => HandleResult(await bills.GetByIdAsync(id, ct));

    [RequirePermission(Permissions.TreasuryLegacy)]
    [HttpPost("{id:guid}/post")]
    public async Task<ActionResult<TaxBillDto>> Post(Guid id, CancellationToken ct) => HandleResult(await bills.PostAsync(id, ct));

    [RequirePermission(Permissions.TreasuryLegacy)]
    [HttpPost("{id:guid}/cancel")]
    public async Task<ActionResult<TaxBillDto>> Cancel(Guid id, [FromBody] CancelBillRequest request, CancellationToken ct) =>
        HandleResult(await bills.CancelAsync(id, request.Reason, ct));

    [RequirePermission(Permissions.TreasuryLegacy)]
    [HttpGet("~/api/properties/{propertyId:guid}/bills")]
    public async Task<ActionResult<IReadOnlyList<TaxBillDto>>> ListByProperty(Guid propertyId, CancellationToken ct) =>
        HandleResult(await bills.ListByPropertyAsync(propertyId, ct));

    [RequirePermission(Permissions.TreasuryLegacy)]
    [HttpGet("~/api/properties/{propertyId:guid}/statement-of-account")]
    public async Task<ActionResult<StatementOfAccountDto>> StatementOfAccount(Guid propertyId, CancellationToken ct) =>
        HandleResult(await bills.GetStatementOfAccountAsync(propertyId, ct));
}
