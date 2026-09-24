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
    string? Remarks);

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
    int ActiveAnnotationCount);

public sealed record TaxDeclarationReasonRequest(string Reason);

public sealed record AddTaxDeclarationAnnotationRequest(
    Guid AnnotationTypeId, string Text, string? ReferenceNumber, DateOnly? ReferenceDate, DateOnly EffectiveDate);

public sealed record LiftTaxDeclarationAnnotationRequest(string Reason, string? Reference);

public sealed record TaxDeclarationAnnotationDto(
    Guid Id, Guid TaxDeclarationId, Guid AnnotationTypeId, string AnnotationTypeCode, string AnnotationTypeName,
    string Text, string? ReferenceNumber, DateOnly? ReferenceDate, DateOnly EffectiveDate,
    DateTimeOffset CreatedAt, Guid? CreatedBy,
    DateTimeOffset? LiftedAt, Guid? LiftedBy, string? LiftReason, string? LiftReference);
