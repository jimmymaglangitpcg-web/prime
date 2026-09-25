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
    Guid AssessmentLevelId,
    decimal AssessmentPercentage,
    decimal AssessedValue,
    WorkflowStatus Status,
    DateOnly EffectiveDate,
    Guid? PreviousAssessmentId,
    Guid? RevisionReference,
    string? Remarks,
    string? FaasNumber,
    Guid? ApprovedBy,
    DateTimeOffset? ApprovedAt,
    DateTimeOffset CreatedAt);
