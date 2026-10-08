using Microsoft.AspNetCore.Mvc;
using Prime.Application.Features.Collection;
using Prime.Domain.Enums;
using Prime.Application.Common.Security;
using Prime.WebApi.Authorization;

namespace Prime.WebApi.Controllers;

/// <summary>
/// Collection (docs/analysis/collection.md §4): what is owed, quote, post,
/// read. The payment date is always today in the LGU's time zone — no
/// back-dating. Role gating (Cashier) is Phase 12.
/// </summary>
[Route("api/payments")]
public class PaymentsController(IPaymentService payments) : ApiControllerBase
{
    [RequirePermission(Permissions.TreasuryLegacy)]
    [HttpGet("~/api/properties/{propertyId:guid}/outstanding")]
    public async Task<ActionResult<OutstandingDto>> Outstanding(Guid propertyId, [FromQuery] DateOnly? asOf, CancellationToken ct) =>
        HandleResult(await payments.GetOutstandingAsync(propertyId, asOf, ct));

    [RequirePermission(Permissions.TreasuryLegacy)]
    [HttpPost("quote")]
    public async Task<ActionResult<PaymentQuoteDto>> Quote(QuotePaymentRequest request, CancellationToken ct) =>
        HandleResult(await payments.QuoteAsync(request, ct));

    [RequirePermission(Permissions.TreasuryLegacy)]
    [HttpPost]
    public async Task<ActionResult<PaymentDto>> Post(PostPaymentRequest request, CancellationToken ct) =>
        HandleCreated(await payments.PostAsync(request, ct), nameof(GetById), dto => new { id = dto.Id });

    [RequirePermission(Permissions.TreasuryLegacy)]
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PaymentDto>> GetById(Guid id, CancellationToken ct) => HandleResult(await payments.GetByIdAsync(id, ct));

    [RequirePermission(Permissions.TreasuryLegacy)]
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PaymentSummaryDto>>> List([FromQuery] DateOnly? date, [FromQuery] Guid? cashierUserId,
        [FromQuery] PaymentStatus? status, CancellationToken ct) =>
        HandleResult(await payments.ListAsync(date, cashierUserId, status, ct));

    [RequirePermission(Permissions.TreasuryLegacy)]
    [HttpPost("{id:guid}/cancellation-requests")]
    public async Task<ActionResult<PaymentCancellationDto>> RequestCancellation(Guid id, RequestPaymentCancellationRequest request, CancellationToken ct) =>
        HandleResult(await payments.RequestCancellationAsync(id, request, ct));

    [RequirePermission(Permissions.TreasuryLegacy)]
    [HttpPost("{id:guid}/correction-requests")]
    public async Task<ActionResult<PaymentCancellationDto>> RequestCorrection(Guid id, RequestPaymentCorrectionRequest request, CancellationToken ct) =>
        HandleResult(await payments.RequestCorrectionAsync(id, request, ct));

    [RequirePermission(Permissions.TreasuryLegacy)]
    [HttpGet("cancellation-requests")]
    public async Task<ActionResult<IReadOnlyList<PaymentCancellationDto>>> Cancellations([FromQuery] PaymentCancellationStatus? status, CancellationToken ct) =>
        HandleResult(await payments.ListCancellationsAsync(status, ct));

    [RequirePermission(Permissions.TreasuryLegacy)]
    [HttpPost("cancellation-requests/{id:guid}/approve")]
    public async Task<ActionResult<PaymentCancellationDto>> ApproveCancellation(Guid id, DecidePaymentCancellationRequest request, CancellationToken ct) =>
        HandleResult(await payments.ApproveCancellationAsync(id, request, ct));

    [RequirePermission(Permissions.TreasuryLegacy)]
    [HttpPost("cancellation-requests/{id:guid}/reject")]
    public async Task<ActionResult<PaymentCancellationDto>> RejectCancellation(Guid id, DecidePaymentCancellationRequest request, CancellationToken ct) =>
        HandleResult(await payments.RejectCancellationAsync(id, request, ct));

    [RequirePermission(Permissions.TreasuryLegacy)]
    [HttpGet("~/api/properties/{propertyId:guid}/payments")]
    public async Task<ActionResult<IReadOnlyList<PaymentSummaryDto>>> ListByProperty(Guid propertyId, CancellationToken ct) =>
        HandleResult(await payments.ListByPropertyAsync(propertyId, ct));
}

/// <summary>Collection configuration: modes of payment and revenue account mappings (maker-checker approval).</summary>
[Route("api/collection")]
public class CollectionSetupController(ICollectionSetupService setup) : ApiControllerBase
{
    [RequirePermission(Permissions.TreasuryLegacy)]
    [HttpGet("payment-modes")]
    public async Task<ActionResult<IReadOnlyList<PaymentModeDto>>> PaymentModes(CancellationToken ct) =>
        HandleResult(await setup.ListPaymentModesAsync(ct));

    [RequirePermission(Permissions.TreasuryLegacy)]
    [HttpPost("payment-modes")]
    public async Task<ActionResult<PaymentModeDto>> CreatePaymentMode(CreatePaymentModeRequest request, CancellationToken ct) =>
        HandleResult(await setup.CreatePaymentModeAsync(request, ct));

    [RequirePermission(Permissions.TreasuryLegacy)]
    [HttpGet("account-mappings")]
    public async Task<ActionResult<IReadOnlyList<RevenueAccountMappingDto>>> AccountMappings(CancellationToken ct) =>
        HandleResult(await setup.ListAccountMappingsAsync(ct));

    [RequirePermission(Permissions.TreasuryLegacy)]
    [HttpPost("account-mappings")]
    public async Task<ActionResult<RevenueAccountMappingDto>> CreateAccountMapping(CreateRevenueAccountMappingRequest request, CancellationToken ct) =>
        HandleResult(await setup.CreateAccountMappingAsync(request, ct));

    [RequirePermission(Permissions.TreasuryLegacy)]
    [HttpPost("account-mappings/{id:guid}/approve")]
    public async Task<ActionResult<RevenueAccountMappingDto>> ApproveAccountMapping(Guid id, CancellationToken ct) =>
        HandleResult(await setup.ApproveAccountMappingAsync(id, ct));
}

/// <summary>Remittance, collection summary and reconciliation (docs/analysis/collection.md §4.7, step 9e).</summary>
[Route("api/collections")]
public class CollectionsController(ICollectionReportService reports) : ApiControllerBase
{
    [RequirePermission(Permissions.TreasuryLegacy)]
    [HttpPost("remittances")]
    public async Task<ActionResult<RemittanceDto>> Remit(CreateRemittanceRequest request, CancellationToken ct) =>
        HandleResult(await reports.CreateRemittanceAsync(request, ct));

    [RequirePermission(Permissions.TreasuryLegacy)]
    [HttpGet("remittances")]
    public async Task<ActionResult<IReadOnlyList<RemittanceDto>>> Remittances([FromQuery] DateOnly? date, [FromQuery] RemittanceStatus? status, CancellationToken ct) =>
        HandleResult(await reports.ListRemittancesAsync(date, status, ct));

    [RequirePermission(Permissions.TreasuryLegacy)]
    [HttpGet("remittances/{id:guid}")]
    public async Task<ActionResult<RemittanceDto>> Remittance(Guid id, CancellationToken ct) => HandleResult(await reports.GetRemittanceAsync(id, ct));

    [RequirePermission(Permissions.TreasuryLegacy)]
    [HttpPost("remittances/{id:guid}/accept")]
    public async Task<ActionResult<RemittanceDto>> Accept(Guid id, DecideRemittanceRequest request, CancellationToken ct) =>
        HandleResult(await reports.AcceptRemittanceAsync(id, request, ct));

    [RequirePermission(Permissions.TreasuryLegacy)]
    [HttpPost("remittances/{id:guid}/return")]
    public async Task<ActionResult<RemittanceDto>> Return(Guid id, DecideRemittanceRequest request, CancellationToken ct) =>
        HandleResult(await reports.ReturnRemittanceAsync(id, request, ct));

    [RequirePermission(Permissions.TreasuryLegacy)]
    [HttpGet("summary")]
    public async Task<ActionResult<CollectionSummaryDto>> Summary([FromQuery] DateOnly from, [FromQuery] DateOnly to, [FromQuery] CollectionGroupBy groupBy,
        CancellationToken ct) =>
        HandleResult(await reports.SummaryAsync(from, to, groupBy, ct));

    [RequirePermission(Permissions.TreasuryLegacy)]
    [HttpGet("reconciliation")]
    public async Task<ActionResult<ReconciliationDto>> Reconciliation([FromQuery] DateOnly? date, CancellationToken ct) =>
        HandleResult(await reports.ReconcileAsync(date, ct));
}
