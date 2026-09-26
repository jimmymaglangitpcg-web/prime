using Prime.Domain.Enums;

namespace Prime.Application.Features.Collection;

// --- What is owed (docs/analysis/collection.md §4.1) ---

public sealed record OutstandingDto(
    Guid PropertyId,
    DateOnly AsOfDate,
    IReadOnlyList<OutstandingBillDto> Bills,
    decimal TotalOutstandingPrincipal,
    /// <summary>What settling everything outstanding would cost on <see cref="AsOfDate"/>.</summary>
    decimal TotalDueAsOf);

public sealed record OutstandingBillDto(
    Guid BillId,
    string? BillNumber,
    Guid RpuId,
    string RpuNumber,
    string TaxDeclarationNumber,
    int TaxYear,
    IReadOnlyList<OutstandingInstallmentDto> Installments);

/// <summary>One installment, all tax types together. <see cref="Overpaid"/>: more was paid than a later bill now asks for.</summary>
public sealed record OutstandingInstallmentDto(
    int InstallmentSequence,
    DateOnly DueDate,
    decimal PrincipalOwed,
    decimal PrincipalPaid,
    decimal Outstanding,
    decimal? DueIfPaidAsOf,
    bool Overpaid);

// --- Quote and post (§4.2–§4.3) ---

/// <summary>A whole installment, or with <see cref="PrincipalAmount"/> a part of its principal.</summary>
public sealed record PaymentItemRequest(Guid RpuId, int TaxYear, int InstallmentSequence, decimal? PrincipalAmount = null);

public sealed record QuotePaymentRequest(IReadOnlyList<PaymentItemRequest> Items);

public sealed record PaymentQuoteDto(DateOnly PaymentDate, IReadOnlyList<PaymentAllocationDto> Allocations, decimal Total);

public sealed record PaymentTenderRequest(Guid PaymentModeId, decimal Amount, string? Reference = null, string? Bank = null, DateOnly? CheckDate = null);

/// <summary>
/// <see cref="ExpectedTotal"/> is the quoted total the cashier confirmed; the
/// server recomputes and refuses if it differs. <see cref="OfficialReceiptNumber"/>
/// is typed only for a pre-printed receipt, when the numbering scheme allows it.
/// </summary>
public sealed record PostPaymentRequest(
    string IdempotencyKey,
    Guid? PayorTaxpayerId,
    string PayorName,
    string? PayorAddress,
    IReadOnlyList<PaymentItemRequest> Items,
    IReadOnlyList<PaymentTenderRequest> Tenders,
    decimal ExpectedTotal,
    string? OfficialReceiptNumber = null,
    string? Remarks = null);

public sealed record PaymentDto(
    Guid Id,
    string TransactionNumber,
    string OfficialReceiptNumber,
    Guid? PayorTaxpayerId,
    string PayorName,
    string? PayorAddress,
    DateOnly PaymentDate,
    DateTimeOffset ReceivedAt,
    string? Office,
    string? LocationCode,
    Guid? CashierUserId,
    decimal AmountDue,
    decimal AmountTendered,
    decimal Change,
    PaymentStatus Status,
    string? Remarks,
    IReadOnlyList<PaymentTenderDto> Tenders,
    IReadOnlyList<PaymentAllocationDto> Allocations);

public sealed record PaymentTenderDto(Guid PaymentModeId, string ModeCode, string ModeName, decimal Amount, string? Reference, string? Bank, DateOnly? CheckDate);

public sealed record PaymentAllocationDto(
    int LineNumber,
    Guid BillId,
    string? BillNumber,
    string TaxDeclarationNumber,
    Guid PropertyId,
    Guid RpuId,
    string RpuNumber,
    int TaxYear,
    int InstallmentSequence,
    DateOnly DueDate,
    Guid TaxTypeId,
    string TaxTypeCode,
    string TaxTypeName,
    BillingComponent Component,
    Guid RuleId,
    decimal? RatePercent,
    decimal BaseAmount,
    decimal Amount,
    int? Months,
    CollectionYearCategory YearCategory,
    string Explanation,
    string AccountCode,
    string AccountName,
    string? Fund);

public sealed record PaymentSummaryDto(
    Guid Id,
    string TransactionNumber,
    string OfficialReceiptNumber,
    string PayorName,
    DateOnly PaymentDate,
    DateTimeOffset ReceivedAt,
    Guid? CashierUserId,
    decimal AmountDue,
    PaymentStatus Status);

// --- Setup: payment modes and revenue account mappings (§3) ---

public sealed record PaymentModeDto(Guid Id, string Code, string Name, string? Description, bool RequiresReference, bool AllowsChange, int SortOrder, bool IsActive);

public sealed record CreatePaymentModeRequest(string Code, string Name, string? Description, bool RequiresReference, bool AllowsChange, int SortOrder = 0);

public sealed record CreateRevenueAccountMappingRequest(
    Guid TaxTypeId,
    BillingComponent Component,
    CollectionYearCategory YearCategory,
    string AccountCode,
    string AccountName,
    string? Fund,
    string LegalBasis,
    DateOnly EffectiveDate,
    string? Remarks = null);

public sealed record RevenueAccountMappingDto(
    Guid Id,
    Guid TaxTypeId,
    string TaxTypeCode,
    BillingComponent Component,
    CollectionYearCategory YearCategory,
    string AccountCode,
    string AccountName,
    string? Fund,
    string LegalBasis,
    DateOnly EffectiveDate,
    DateOnly? EndDate,
    WorkflowStatus Status,
    Guid? CreatedBy,
    Guid? ApprovedBy,
    DateTimeOffset? ApprovedAt,
    string? Remarks);
