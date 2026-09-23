using Prime.Domain.Enums;

namespace Prime.Application.Features.AssessmentLevels;

public sealed record CreateAssessmentLevelRequest(
    string OrdinanceNumber,
    DateOnly? OrdinanceDate,
    Guid ClassificationId,
    Guid ActualUseId,
    Guid PropertyTypeId,
    decimal LowerValue,
    decimal? UpperValue,
    decimal AssessmentPercentage,
    DateOnly EffectiveDate);

public sealed record AssessmentLevelDto(
    Guid Id,
    string OrdinanceNumber,
    DateOnly? OrdinanceDate,
    Guid ClassificationId,
    string ClassificationName,
    Guid ActualUseId,
    string ActualUseName,
    Guid PropertyTypeId,
    string PropertyTypeName,
    decimal LowerValue,
    decimal? UpperValue,
    decimal AssessmentPercentage,
    DateOnly EffectiveDate,
    DateOnly? EndDate,
    WorkflowStatus Status,
    DateTimeOffset CreatedAt);
