using Prime.Domain.Enums;

namespace Prime.Application.Features.Billing.Bills;

public sealed record GenerateBillRequest(Guid RpuId, int TaxYear, DateOnly AsOfDate);

public sealed record CancelBillRequest(string Reason);

public sealed record TaxBillDto(
    Guid Id,
    Guid PropertyId,
    Guid RpuId,
    string RpuNumber,
    Guid TaxDeclarationId,
    string TaxDeclarationNumber,
    Guid AssessmentId,
    string? BillNumber,
    int TaxYear,
    DateOnly AsOfDate,
    DateOnly RulesAsOfDate,
    decimal AssessedValue,
    Guid ClassificationId,
    bool DiscountStackingAllowed,
    string? Notes,
    WorkflowStatus Status,
    DateTimeOffset CreatedAt,
    Guid? CreatedBy,
    DateTimeOffset? PostedAt,
    Guid? PostedBy,
    DateTimeOffset? CancelledAt,
    string? CancellationReason,
    Guid? SupersededByBillId,
    decimal Total,
    IReadOnlyList<TaxBillTaxTypeDto> TaxTypes,
    IReadOnlyList<TaxBillDetailDto> Details);

public sealed record TaxBillTaxTypeDto(
    Guid TaxTypeId,
    string TaxTypeCode,
    string TaxTypeName,
    Guid TaxRateId,
    decimal RatePercent,
    decimal ComputedAnnualTax,
    Guid? CapRuleId,
    decimal? CapBaselineTax,
    decimal? CapLimit,
    decimal AnnualTax,
    IReadOnlyList<TaxBillTaxTypeLineDto> Lines);

/// <summary>The tax one assessment line bears for this tax type, at its classification's rate.</summary>
public sealed record TaxBillTaxTypeLineDto(Guid? ClassificationId, decimal AssessedValue, Guid TaxRateId, decimal RatePercent, decimal Tax);

public sealed record TaxBillDetailDto(
    int LineNumber,
    int InstallmentSequence,
    DateOnly DueDate,
    Guid TaxTypeId,
    string TaxTypeCode,
    BillingComponent Component,
    Guid RuleId,
    decimal? RatePercent,
    decimal BaseAmount,
    decimal Amount,
    int? Months,
    string Explanation);

/// <summary>
/// Statement of account (CLAUDE.md §52): the posted bill per RPU and tax year
/// for a property. Payments are Phase 9, so the balance is not yet
/// reduced by anything paid.
/// </summary>
/// <summary>
/// Posted bills with what has been paid on them and what settling the rest
/// would cost on <see cref="AsOfDate"/> (docs/analysis/collection.md §6), plus
/// the property's receipts.
/// </summary>
public sealed record StatementOfAccountDto(
    Guid PropertyId,
    string PropertyIdentificationNumber,
    DateTimeOffset GeneratedAt,
    DateOnly AsOfDate,
    IReadOnlyList<StatementLineDto> Bills,
    decimal TotalBilled,
    decimal TotalPrincipalPaid,
    decimal TotalOutstandingPrincipal,
    decimal TotalDueAsOf,
    IReadOnlyList<StatementPaymentDto> Payments);

public sealed record StatementPaymentDto(Guid PaymentId, string OfficialReceiptNumber, DateOnly PaymentDate, string PayorName, decimal Amount, string Status);

public sealed record StatementLineDto(
    Guid BillId,
    Guid RpuId,
    string RpuNumber,
    string TaxDeclarationNumber,
    int TaxYear,
    DateOnly AsOfDate,
    decimal AssessedValue,
    decimal Tax,
    decimal Discount,
    decimal Penalty,
    decimal Interest,
    decimal Total,
    /// <summary>Principal (tax) owed on this bill and what standing payments have settled of it.</summary>
    decimal PrincipalOwed,
    decimal PrincipalPaid,
    decimal OutstandingPrincipal,
    /// <summary>Settling what is outstanding on the statement's as-of date; 0 when nothing is.</summary>
    decimal DueAsOf);
