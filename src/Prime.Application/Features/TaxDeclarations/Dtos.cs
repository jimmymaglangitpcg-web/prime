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
    DateTimeOffset CreatedAt);
