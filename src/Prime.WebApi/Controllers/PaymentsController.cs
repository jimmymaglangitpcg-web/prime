using Microsoft.AspNetCore.Mvc;
using Prime.Application.Features.Collection;
using Prime.Domain.Enums;

namespace Prime.WebApi.Controllers;

/// <summary>
/// Collection (docs/analysis/collection.md §4): what is owed, quote, post,
/// read. The payment date is always today in the LGU's time zone — no
/// back-dating. Role gating (Cashier) is Phase 12.
/// </summary>
[Route("api/payments")]
public class PaymentsController(IPaymentService payments) : ApiControllerBase
{
    [HttpGet("~/api/properties/{propertyId:guid}/outstanding")]
    public async Task<ActionResult<OutstandingDto>> Outstanding(Guid propertyId, [FromQuery] DateOnly? asOf, CancellationToken ct) =>
        HandleResult(await payments.GetOutstandingAsync(propertyId, asOf, ct));

    [HttpPost("quote")]
    public async Task<ActionResult<PaymentQuoteDto>> Quote(QuotePaymentRequest request, CancellationToken ct) =>
        HandleResult(await payments.QuoteAsync(request, ct));

    [HttpPost]
    public async Task<ActionResult<PaymentDto>> Post(PostPaymentRequest request, CancellationToken ct) =>
        HandleCreated(await payments.PostAsync(request, ct), nameof(GetById), dto => new { id = dto.Id });

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PaymentDto>> GetById(Guid id, CancellationToken ct) => HandleResult(await payments.GetByIdAsync(id, ct));

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PaymentSummaryDto>>> List([FromQuery] DateOnly? date, [FromQuery] Guid? cashierUserId,
        [FromQuery] PaymentStatus? status, CancellationToken ct) =>
        HandleResult(await payments.ListAsync(date, cashierUserId, status, ct));

    [HttpGet("~/api/properties/{propertyId:guid}/payments")]
    public async Task<ActionResult<IReadOnlyList<PaymentSummaryDto>>> ListByProperty(Guid propertyId, CancellationToken ct) =>
        HandleResult(await payments.ListByPropertyAsync(propertyId, ct));
}

/// <summary>Collection configuration: modes of payment and revenue account mappings (maker-checker approval).</summary>
[Route("api/collection")]
public class CollectionSetupController(ICollectionSetupService setup) : ApiControllerBase
{
    [HttpGet("payment-modes")]
    public async Task<ActionResult<IReadOnlyList<PaymentModeDto>>> PaymentModes(CancellationToken ct) =>
        HandleResult(await setup.ListPaymentModesAsync(ct));

    [HttpPost("payment-modes")]
    public async Task<ActionResult<PaymentModeDto>> CreatePaymentMode(CreatePaymentModeRequest request, CancellationToken ct) =>
        HandleResult(await setup.CreatePaymentModeAsync(request, ct));

    [HttpGet("account-mappings")]
    public async Task<ActionResult<IReadOnlyList<RevenueAccountMappingDto>>> AccountMappings(CancellationToken ct) =>
        HandleResult(await setup.ListAccountMappingsAsync(ct));

    [HttpPost("account-mappings")]
    public async Task<ActionResult<RevenueAccountMappingDto>> CreateAccountMapping(CreateRevenueAccountMappingRequest request, CancellationToken ct) =>
        HandleResult(await setup.CreateAccountMappingAsync(request, ct));

    [HttpPost("account-mappings/{id:guid}/approve")]
    public async Task<ActionResult<RevenueAccountMappingDto>> ApproveAccountMapping(Guid id, CancellationToken ct) =>
        HandleResult(await setup.ApproveAccountMappingAsync(id, ct));
}
