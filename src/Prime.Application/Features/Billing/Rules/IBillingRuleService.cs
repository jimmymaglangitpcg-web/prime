using Prime.Application.Common;

namespace Prime.Application.Features.Billing.Rules;

/// <summary>Billing rule administration — docs/BILLING.md §3. See <see cref="BillingRuleService"/>.</summary>
public interface IBillingRuleService
{
    Task<Result<TaxRateDto>> CreateTaxRateAsync(CreateTaxRateRequest request, CancellationToken cancellationToken = default);
    Task<Result<TaxRateDto>> ApproveTaxRateAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<TaxRateDto>> GetTaxRateAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<TaxRateDto>>> ListTaxRatesAsync(DateOnly? asOf, CancellationToken cancellationToken = default);
    Task<Result<PaymentScheduleDto>> CreatePaymentScheduleAsync(CreatePaymentScheduleRequest request, CancellationToken cancellationToken = default);
    Task<Result<PaymentScheduleDto>> ApprovePaymentScheduleAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<PaymentScheduleDto>> GetPaymentScheduleAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<PaymentScheduleDto>>> ListPaymentSchedulesAsync(DateOnly? asOf, CancellationToken cancellationToken = default);
    Task<Result<DiscountRuleDto>> CreateDiscountRuleAsync(CreateDiscountRuleRequest request, CancellationToken cancellationToken = default);
    Task<Result<DiscountRuleDto>> ApproveDiscountRuleAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<DiscountRuleDto>> GetDiscountRuleAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<DiscountRuleDto>>> ListDiscountRulesAsync(DateOnly? asOf, CancellationToken cancellationToken = default);
    Task<Result<InterestRuleDto>> CreateInterestRuleAsync(CreateInterestRuleRequest request, CancellationToken cancellationToken = default);
    Task<Result<InterestRuleDto>> ApproveInterestRuleAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<InterestRuleDto>> GetInterestRuleAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<InterestRuleDto>>> ListInterestRulesAsync(DateOnly? asOf, CancellationToken cancellationToken = default);
    Task<Result<PenaltyRuleDto>> CreatePenaltyRuleAsync(CreatePenaltyRuleRequest request, CancellationToken cancellationToken = default);
    Task<Result<PenaltyRuleDto>> ApprovePenaltyRuleAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<PenaltyRuleDto>> GetPenaltyRuleAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<PenaltyRuleDto>>> ListPenaltyRulesAsync(DateOnly? asOf, CancellationToken cancellationToken = default);
    Task<Result<TaxIncreaseCapRuleDto>> CreateTaxIncreaseCapRuleAsync(CreateTaxIncreaseCapRuleRequest request, CancellationToken cancellationToken = default);
    Task<Result<TaxIncreaseCapRuleDto>> ApproveTaxIncreaseCapRuleAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<TaxIncreaseCapRuleDto>> GetTaxIncreaseCapRuleAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<TaxIncreaseCapRuleDto>>> ListTaxIncreaseCapRulesAsync(DateOnly? asOf, CancellationToken cancellationToken = default);
}
