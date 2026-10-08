using Prime.Domain.Enums;

namespace Prime.Application.Features.TaxDeclarations;

public sealed record CreateTaxDeclarationRequest(
    Guid RpuId,
    string? TaxDeclarationNumber,
    DateOnly EffectivityDate,
    Taxability Taxability,
    Guid ClassificationId,
    Guid ActualUseId,
    Guid? SubClassificationId,
    int AssessmentYear,
    Guid? PreviousTaxDeclarationId,
    string? Remarks,
    Guid? PropertyTransactionId = null,
    Guid? AssessmentId = null,
    string? TransactionCode = null,
    /// <summary>Within a court-order transaction: the cancelled TD this new declaration restores (§4.3).</summary>
    Guid? RestoresTaxDeclarationId = null);

public sealed record TaxDeclarationDto(
    Guid Id,
    Guid RpuId,
    Guid PropertyId,
    string TaxDeclarationNumber,
    int RevisionNumber,
    DateOnly EffectivityDate,
    Taxability Taxability,
    Guid ClassificationId,
    string ClassificationName,
    Guid ActualUseId,
    string ActualUseName,
    Guid? SubClassificationId,
    int AssessmentYear,
    WorkflowStatus Status,
    Guid? PreviousTaxDeclarationId,
    string? Remarks,
    DateTimeOffset CreatedAt,
    Guid? CreatedBy,
    Guid? ApprovedBy,
    DateTimeOffset? ApprovedAt,
    DateTimeOffset? CancelledAt,
    string? CancellationReason,
    Guid? SupersededByTaxDeclarationId,
    int ActiveAnnotationCount,
    Guid? PropertyTransactionId,
    Guid? AssessmentId,
    string? FaasNumber,
    string? TransactionCode = null,
    int? TransactionRank = null,
    /// <summary>The assessment count the TD number was made from (identification-numbering.md §4.1).</summary>
    long? AssessmentCount = null,
    Guid? RestoresTaxDeclarationId = null,
    /// <summary>The cancellation waiting for a second user's decision, if any (workflow-security.md Q19).</summary>
    TaxDeclarationCancellationRequestDto? OpenCancellationRequest = null);

/// <summary>A request to cancel an approved TD outright, decided by a second user (workflow-security.md Q19).</summary>
public sealed record TaxDeclarationCancellationRequestDto(
    Guid Id, Guid TaxDeclarationId, string Reason, WorkflowStatus Status, DateTimeOffset RequestedAt, Guid? RequestedBy,
    DateTimeOffset? DecidedAt, Guid? DecidedBy, string? DecisionReason);

public sealed record TaxDeclarationReasonRequest(string Reason);

public sealed record AddTaxDeclarationAnnotationRequest(
    Guid AnnotationTypeId, string Text, string? ReferenceNumber, DateOnly? ReferenceDate, DateOnly EffectiveDate);

public sealed record LiftTaxDeclarationAnnotationRequest(string Reason, string? Reference);

public sealed record TaxDeclarationAnnotationDto(
    Guid Id, Guid TaxDeclarationId, Guid AnnotationTypeId, string AnnotationTypeCode, string AnnotationTypeName,
    string Text, string? ReferenceNumber, DateOnly? ReferenceDate, DateOnly EffectiveDate,
    DateTimeOffset CreatedAt, Guid? CreatedBy,
    DateTimeOffset? LiftedAt, Guid? LiftedBy, string? LiftReason, string? LiftReference,
    Guid? CarriedFromAnnotationId = null, string? CarriedFromTaxDeclarationNumber = null);
