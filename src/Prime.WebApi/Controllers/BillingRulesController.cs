using Microsoft.AspNetCore.Mvc;
using Prime.Application.Features.Billing.Rules;
using Prime.Application.Common.Security;
using Prime.WebApi.Authorization;

namespace Prime.WebApi.Controllers;

/// <summary>
/// Billing rule administration (docs/BILLING.md §3). Each rule type: create
/// (Draft), approve (maker-checker; closes its predecessor), get, and list —
/// all rules, or with ?asOf= only the approved rules in force on that date.
/// Role gating (Treasurer/Assessor) is Phase 12.
/// </summary>
[Route("api/billing")]
public class BillingRulesController(IBillingRuleService rules) : ApiControllerBase
{
    [RequirePermission(Permissions.TreasuryLegacy)]
    [HttpGet("tax-rates")]
    public async Task<ActionResult<IReadOnlyList<TaxRateDto>>> ListTaxRates([FromQuery] DateOnly? asOf, CancellationToken ct) =>
        HandleResult(await rules.ListTaxRatesAsync(asOf, ct));

    [RequirePermission(Permissions.TreasuryLegacy)]
    [HttpGet("tax-rates/{id:guid}")]
    public async Task<ActionResult<TaxRateDto>> GetTaxRate(Guid id, CancellationToken ct) => HandleResult(await rules.GetTaxRateAsync(id, ct));

    [RequirePermission(Permissions.TreasuryLegacy)]
    [HttpPost("tax-rates")]
    public async Task<ActionResult<TaxRateDto>> CreateTaxRate(CreateTaxRateRequest request, CancellationToken ct) =>
        HandleCreated(await rules.CreateTaxRateAsync(request, ct), nameof(GetTaxRate), dto => new { id = dto.Rule.Id });

    [RequirePermission(Permissions.TreasuryLegacy)]
    [HttpPost("tax-rates/{id:guid}/approve")]
    public async Task<ActionResult<TaxRateDto>> ApproveTaxRate(Guid id, CancellationToken ct) => HandleResult(await rules.ApproveTaxRateAsync(id, ct));

    [RequirePermission(Permissions.TreasuryLegacy)]
    [HttpGet("payment-schedules")]
    public async Task<ActionResult<IReadOnlyList<PaymentScheduleDto>>> ListPaymentSchedules([FromQuery] DateOnly? asOf, CancellationToken ct) =>
        HandleResult(await rules.ListPaymentSchedulesAsync(asOf, ct));

    [RequirePermission(Permissions.TreasuryLegacy)]
    [HttpGet("payment-schedules/{id:guid}")]
    public async Task<ActionResult<PaymentScheduleDto>> GetPaymentSchedule(Guid id, CancellationToken ct) => HandleResult(await rules.GetPaymentScheduleAsync(id, ct));

    [RequirePermission(Permissions.TreasuryLegacy)]
    [HttpPost("payment-schedules")]
    public async Task<ActionResult<PaymentScheduleDto>> CreatePaymentSchedule(CreatePaymentScheduleRequest request, CancellationToken ct) =>
        HandleCreated(await rules.CreatePaymentScheduleAsync(request, ct), nameof(GetPaymentSchedule), dto => new { id = dto.Rule.Id });

    [RequirePermission(Permissions.TreasuryLegacy)]
    [HttpPost("payment-schedules/{id:guid}/approve")]
    public async Task<ActionResult<PaymentScheduleDto>> ApprovePaymentSchedule(Guid id, CancellationToken ct) => HandleResult(await rules.ApprovePaymentScheduleAsync(id, ct));

    [RequirePermission(Permissions.TreasuryLegacy)]
    [HttpGet("discount-rules")]
    public async Task<ActionResult<IReadOnlyList<DiscountRuleDto>>> ListDiscountRules([FromQuery] DateOnly? asOf, CancellationToken ct) =>
        HandleResult(await rules.ListDiscountRulesAsync(asOf, ct));

    [RequirePermission(Permissions.TreasuryLegacy)]
    [HttpGet("discount-rules/{id:guid}")]
    public async Task<ActionResult<DiscountRuleDto>> GetDiscountRule(Guid id, CancellationToken ct) => HandleResult(await rules.GetDiscountRuleAsync(id, ct));

    [RequirePermission(Permissions.TreasuryLegacy)]
    [HttpPost("discount-rules")]
    public async Task<ActionResult<DiscountRuleDto>> CreateDiscountRule(CreateDiscountRuleRequest request, CancellationToken ct) =>
        HandleCreated(await rules.CreateDiscountRuleAsync(request, ct), nameof(GetDiscountRule), dto => new { id = dto.Rule.Id });

    [RequirePermission(Permissions.TreasuryLegacy)]
    [HttpPost("discount-rules/{id:guid}/approve")]
    public async Task<ActionResult<DiscountRuleDto>> ApproveDiscountRule(Guid id, CancellationToken ct) => HandleResult(await rules.ApproveDiscountRuleAsync(id, ct));

    [RequirePermission(Permissions.TreasuryLegacy)]
    [HttpGet("interest-rules")]
    public async Task<ActionResult<IReadOnlyList<InterestRuleDto>>> ListInterestRules([FromQuery] DateOnly? asOf, CancellationToken ct) =>
        HandleResult(await rules.ListInterestRulesAsync(asOf, ct));

    [RequirePermission(Permissions.TreasuryLegacy)]
    [HttpGet("interest-rules/{id:guid}")]
    public async Task<ActionResult<InterestRuleDto>> GetInterestRule(Guid id, CancellationToken ct) => HandleResult(await rules.GetInterestRuleAsync(id, ct));

    [RequirePermission(Permissions.TreasuryLegacy)]
    [HttpPost("interest-rules")]
    public async Task<ActionResult<InterestRuleDto>> CreateInterestRule(CreateInterestRuleRequest request, CancellationToken ct) =>
        HandleCreated(await rules.CreateInterestRuleAsync(request, ct), nameof(GetInterestRule), dto => new { id = dto.Rule.Id });

    [RequirePermission(Permissions.TreasuryLegacy)]
    [HttpPost("interest-rules/{id:guid}/approve")]
    public async Task<ActionResult<InterestRuleDto>> ApproveInterestRule(Guid id, CancellationToken ct) => HandleResult(await rules.ApproveInterestRuleAsync(id, ct));

    [RequirePermission(Permissions.TreasuryLegacy)]
    [HttpGet("penalty-rules")]
    public async Task<ActionResult<IReadOnlyList<PenaltyRuleDto>>> ListPenaltyRules([FromQuery] DateOnly? asOf, CancellationToken ct) =>
        HandleResult(await rules.ListPenaltyRulesAsync(asOf, ct));

    [RequirePermission(Permissions.TreasuryLegacy)]
    [HttpGet("penalty-rules/{id:guid}")]
    public async Task<ActionResult<PenaltyRuleDto>> GetPenaltyRule(Guid id, CancellationToken ct) => HandleResult(await rules.GetPenaltyRuleAsync(id, ct));

    [RequirePermission(Permissions.TreasuryLegacy)]
    [HttpPost("penalty-rules")]
    public async Task<ActionResult<PenaltyRuleDto>> CreatePenaltyRule(CreatePenaltyRuleRequest request, CancellationToken ct) =>
        HandleCreated(await rules.CreatePenaltyRuleAsync(request, ct), nameof(GetPenaltyRule), dto => new { id = dto.Rule.Id });

    [RequirePermission(Permissions.TreasuryLegacy)]
    [HttpPost("penalty-rules/{id:guid}/approve")]
    public async Task<ActionResult<PenaltyRuleDto>> ApprovePenaltyRule(Guid id, CancellationToken ct) => HandleResult(await rules.ApprovePenaltyRuleAsync(id, ct));

    [RequirePermission(Permissions.TreasuryLegacy)]
    [HttpGet("tax-increase-caps")]
    public async Task<ActionResult<IReadOnlyList<TaxIncreaseCapRuleDto>>> ListTaxIncreaseCaps([FromQuery] DateOnly? asOf, CancellationToken ct) =>
        HandleResult(await rules.ListTaxIncreaseCapRulesAsync(asOf, ct));

    [RequirePermission(Permissions.TreasuryLegacy)]
    [HttpGet("tax-increase-caps/{id:guid}")]
    public async Task<ActionResult<TaxIncreaseCapRuleDto>> GetTaxIncreaseCap(Guid id, CancellationToken ct) => HandleResult(await rules.GetTaxIncreaseCapRuleAsync(id, ct));

    [RequirePermission(Permissions.TreasuryLegacy)]
    [HttpPost("tax-increase-caps")]
    public async Task<ActionResult<TaxIncreaseCapRuleDto>> CreateTaxIncreaseCap(CreateTaxIncreaseCapRuleRequest request, CancellationToken ct) =>
        HandleCreated(await rules.CreateTaxIncreaseCapRuleAsync(request, ct), nameof(GetTaxIncreaseCap), dto => new { id = dto.Rule.Id });

    [RequirePermission(Permissions.TreasuryLegacy)]
    [HttpPost("tax-increase-caps/{id:guid}/approve")]
    public async Task<ActionResult<TaxIncreaseCapRuleDto>> ApproveTaxIncreaseCap(Guid id, CancellationToken ct) => HandleResult(await rules.ApproveTaxIncreaseCapRuleAsync(id, ct));
}
