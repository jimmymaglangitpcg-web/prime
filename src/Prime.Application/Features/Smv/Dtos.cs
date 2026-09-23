using Prime.Domain.Enums;

namespace Prime.Application.Features.Smv;

public sealed record CreateSmvRequest(
    string OrdinanceNumber,
    DateOnly OrdinanceDate,
    DateOnly? ApprovalDate,
    DateOnly EffectivityDate,
    int RevisionYear,
    string? Description);

public sealed record SmvDto(
    Guid Id,
    string OrdinanceNumber,
    DateOnly OrdinanceDate,
    DateOnly? ApprovalDate,
    DateOnly EffectivityDate,
    int RevisionYear,
    WorkflowStatus Status,
    string? Description,
    DateTimeOffset CreatedAt);

public sealed record CreateSmvScheduleRequest(
    Guid ClassificationId,
    Guid ActualUseId,
    Guid PropertyTypeId,
    Guid? ZoneId,
    string Unit,
    decimal MarketValue,
    decimal? MinimumValue,
    decimal? MaximumValue,
    DateOnly EffectiveDate);

public sealed record SmvScheduleDto(
    Guid Id,
    Guid SmvId,
    Guid ClassificationId,
    string ClassificationName,
    Guid ActualUseId,
    string ActualUseName,
    Guid PropertyTypeId,
    string PropertyTypeName,
    Guid? ZoneId,
    string? ZoneName,
    string Unit,
    decimal MarketValue,
    decimal? MinimumValue,
    decimal? MaximumValue,
    DateOnly EffectiveDate,
    DateOnly? EndDate,
    WorkflowStatus Status,
    DateTimeOffset CreatedAt);
