using Prime.Domain.Enums;

namespace Prime.Application.Features.Billing.Rules;

/// <summary>Fields every billing rule request carries (docs/BILLING.md §3).</summary>
public interface IBillingRuleRequest
{
    string LegalBasis { get; }
    string OrdinanceNumber { get; }
    DateOnly? OrdinanceDate { get; }
    DateOnly EffectiveDate { get; }
    string? Remarks { get; }
}

public sealed record CreateTaxRateRequest(
    string LegalBasis, string OrdinanceNumber, DateOnly? OrdinanceDate, DateOnly EffectiveDate, string? Remarks,
    Guid TaxTypeId, Guid? ClassificationId, decimal Rate) : IBillingRuleRequest;

public sealed record InstallmentRequest(int Sequence, int DueMonth, int DueDay, decimal SharePercent);

public sealed record CreatePaymentScheduleRequest(
    string LegalBasis, string OrdinanceNumber, DateOnly? OrdinanceDate, DateOnly EffectiveDate, string? Remarks,
    IReadOnlyList<InstallmentRequest> Installments) : IBillingRuleRequest;

public sealed record CreateDiscountRuleRequest(
    string LegalBasis, string OrdinanceNumber, DateOnly? OrdinanceDate, DateOnly EffectiveDate, string? Remarks,
    Guid? TaxTypeId, DiscountKind Kind, decimal Rate, int? CutoffMonth, int? CutoffDay, int? CutoffYearOffset) : IBillingRuleRequest;

public sealed record CreateInterestRuleRequest(
    string LegalBasis, string OrdinanceNumber, DateOnly? OrdinanceDate, DateOnly EffectiveDate, string? Remarks,
    Guid? TaxTypeId, decimal RatePerMonth, int? MaxMonths, InterestMonthCounting MonthCounting) : IBillingRuleRequest;

public sealed record CreatePenaltyRuleRequest(
    string LegalBasis, string OrdinanceNumber, DateOnly? OrdinanceDate, DateOnly EffectiveDate, string? Remarks,
    Guid? TaxTypeId, decimal? Rate, decimal? FixedAmount, int AppliesAfterDays) : IBillingRuleRequest;

/// <summary>EndDate is required here: a cap covers a bounded window (docs/BILLING.md §3.7).</summary>
public sealed record CreateTaxIncreaseCapRuleRequest(
    string LegalBasis, string OrdinanceNumber, DateOnly? OrdinanceDate, DateOnly EffectiveDate, string? Remarks,
    DateOnly EndDate, Guid SmvId, Guid? TaxTypeId, TaxIncreaseCapBasis Basis, TaxIncreaseCapBaseline Baseline,
    decimal MaxIncreasePercent) : IBillingRuleRequest;

/// <summary>Common rule fields, returned nested in every rule DTO.</summary>
public sealed record BillingRuleHeaderDto(
    Guid Id,
    string LegalBasis,
    string OrdinanceNumber,
    DateOnly? OrdinanceDate,
    DateOnly EffectiveDate,
    DateOnly? EndDate,
    WorkflowStatus Status,
    Guid? CreatedBy,
    DateTimeOffset CreatedAt,
    Guid? ApprovedBy,
    DateTimeOffset? ApprovedAt,
    string? Remarks);

public sealed record TaxTypeRefDto(Guid Id, string Code, string Name);

public sealed record TaxRateDto(BillingRuleHeaderDto Rule, TaxTypeRefDto TaxType, Guid? ClassificationId, string? ClassificationName, decimal Rate);

public sealed record InstallmentDto(int Sequence, int DueMonth, int DueDay, decimal SharePercent);

public sealed record PaymentScheduleDto(BillingRuleHeaderDto Rule, IReadOnlyList<InstallmentDto> Installments);

public sealed record DiscountRuleDto(
    BillingRuleHeaderDto Rule, TaxTypeRefDto? TaxType, DiscountKind Kind, decimal Rate, int? CutoffMonth, int? CutoffDay, int? CutoffYearOffset);

public sealed record InterestRuleDto(
    BillingRuleHeaderDto Rule, TaxTypeRefDto? TaxType, decimal RatePerMonth, int? MaxMonths, InterestMonthCounting MonthCounting);

public sealed record PenaltyRuleDto(
    BillingRuleHeaderDto Rule, TaxTypeRefDto? TaxType, decimal? Rate, decimal? FixedAmount, int AppliesAfterDays);

public sealed record SmvRefDto(Guid Id, string OrdinanceNumber, DateOnly EffectivityDate, int RevisionYear);

public sealed record TaxIncreaseCapRuleDto(
    BillingRuleHeaderDto Rule, SmvRefDto Smv, TaxTypeRefDto? TaxType, TaxIncreaseCapBasis Basis, TaxIncreaseCapBaseline Baseline,
    decimal MaxIncreasePercent);
