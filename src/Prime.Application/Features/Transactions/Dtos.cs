using Prime.Domain.Enums;

namespace Prime.Application.Features.Transactions;

// --- Catalogue ---

public sealed record TransactionRequirementRequest(int Sequence, string Code, string Label, bool IsMandatory, string? LegalBasis);

public sealed record CreateTransactionTypeRequest(
    string LegalBasis, DateOnly EffectiveDate, string? Remarks,
    string Code, string Name, PropertyTransactionKind Kind, int? Rank, string? Description,
    IReadOnlyList<TransactionRequirementRequest> Requirements);

public sealed record TransactionRequirementDto(int Sequence, string Code, string Label, bool IsMandatory, string? LegalBasis);

public sealed record TransactionTypeDto(
    Guid Id, string Code, string Name, PropertyTransactionKind Kind, int? Rank, string? Description,
    IReadOnlyList<TransactionRequirementDto> Requirements,
    string LegalBasis, DateOnly EffectiveDate, DateOnly? EndDate, WorkflowStatus Status,
    Guid? CreatedBy, DateTimeOffset CreatedAt, Guid? ApprovedBy, DateTimeOffset? ApprovedAt, string? Remarks);

// --- Transactions ---

public sealed record NewPartyRequest(PropertyPartyRole Role, Guid? TaxpayerId, Guid? OwnershipTypeId, decimal OwnershipPercentage);

public sealed record RelatedPropertyRequest(Guid PropertyId, TransactionPropertyRole Role);

public sealed record OpenTransactionRequest(
    Guid TransactionTypeId,
    Guid PropertyId,
    DateOnly EffectiveDate,
    string Description,
    IReadOnlyList<NewPartyRequest>? NewParties = null,
    IReadOnlyList<Guid>? CancelTaxDeclarationIds = null,
    IReadOnlyList<RelatedPropertyRequest>? RelatedProperties = null);

public sealed record SatisfyRequirementRequest(string EvidenceReference, string? Note);

public sealed record TransactionReasonRequest(string Reason);

public sealed record TransactionRequirementStatusDto(
    Guid Id, int Sequence, string Code, string Label, bool IsMandatory, string? LegalBasis,
    DateTimeOffset? SatisfiedAt, Guid? SatisfiedBy, string? EvidenceReference, string? Note);

public sealed record TransactionPartyDto(Guid Id, PropertyPartyRole Role, Guid? TaxpayerId, string Name, Guid? OwnershipTypeId, decimal OwnershipPercentage);

public sealed record TransactionTdDto(Guid TaxDeclarationId, string TaxDeclarationNumber, Guid PropertyId, WorkflowStatus Status);

public sealed record TransactionPropertyDto(Guid PropertyId, string PropertyIdentificationNumber, TransactionPropertyRole Role);

public sealed record PropertyTransactionDto(
    Guid Id, string? TransactionNumber, Guid TransactionTypeId, string TypeCode, string TypeName, PropertyTransactionKind Kind,
    Guid PropertyId, string PropertyIdentificationNumber, DateOnly EffectiveDate, string Description,
    WorkflowStatus Status, DateTimeOffset CreatedAt, Guid? CreatedBy, DateTimeOffset? SubmittedAt,
    Guid? ApprovedBy, DateTimeOffset? ApprovedAt, DateTimeOffset? ClosedAt, string? CloseReason,
    IReadOnlyList<TransactionRequirementStatusDto> Requirements,
    IReadOnlyList<TransactionPartyDto> NewParties,
    IReadOnlyList<TransactionTdDto> IssuedTaxDeclarations,
    IReadOnlyList<TransactionTdDto> CancelledTaxDeclarations,
    IReadOnlyList<TransactionPropertyDto> RelatedProperties);
