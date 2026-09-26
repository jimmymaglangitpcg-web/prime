using Prime.Domain.Enums;

namespace Prime.Application.Features.Assessments;

public sealed record CreateAssessmentRequest(
    Guid ValuationId,
    int AssessmentYear,
    DateOnly EffectiveDate,
    Guid? PreviousAssessmentId,
    Guid? RevisionReference,
    string? Remarks);

public sealed record AssessmentDto(
    Guid Id,
    Guid RpuId,
    Guid PropertyId,
    Guid ValuationId,
    int AssessmentYear,
    decimal MarketValue,
    Guid? AssessmentLevelId,
    decimal? AssessmentPercentage,
    decimal AssessedValue,
    WorkflowStatus Status,
    DateOnly EffectiveDate,
    Guid? PreviousAssessmentId,
    Guid? RevisionReference,
    string? Remarks,
    string? FaasNumber,
    Guid? ApprovedBy,
    DateTimeOffset? ApprovedAt,
    DateTimeOffset CreatedAt,
    IReadOnlyList<AssessmentLineDto> Lines,
    DateTimeOffset? PostedAt = null,
    Guid? PostedBy = null,
    /// <summary>Set by posting only: whether a draft Tax Declaration was prepared, and if not, why.</summary>
    string? TaxDeclarationNote = null);

/// <summary>A FAAS "Property Assessment" row (docs/analysis/mrpaao-forms-model.md §8.2).</summary>
public sealed record AssessmentLineDto(
    Guid Id, int Sequence, Guid ClassificationId, string ClassificationName, Guid ActualUseId, string ActualUseName,
    decimal MarketValue, Guid AssessmentLevelId, decimal AssessmentPercentage, decimal AssessedValue);

/// <summary>What an assessment of a valuation would be, not saved (docs/analysis/value-and-assess.md §2.3).</summary>
public sealed record AssessmentPreviewDto(Guid ValuationId, Guid RpuId, decimal MarketValue, decimal AssessedValue, IReadOnlyList<AssessmentLineDto> Lines);
