using Prime.Domain.Enums;

namespace Prime.Application.Features.GeneralRevision;

/// <param name="EffectiveDate">The revision's effectivity; the RPUs are valued and assessed as of it.</param>
public sealed record StartGeneralRevisionRequest(IReadOnlyList<Guid> RpuIds, int RevisionYear, DateOnly EffectiveDate);

public sealed record GeneralRevisionJobDto(
    Guid Id,
    int RevisionYear,
    DateOnly? EffectiveDate,
    JobExecutionStatus Status,
    int TotalCount,
    int ProcessedCount,
    int FailedCount,
    Guid? StartedBy,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    string? Remarks);
