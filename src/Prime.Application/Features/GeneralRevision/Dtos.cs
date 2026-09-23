using Prime.Domain.Enums;

namespace Prime.Application.Features.GeneralRevision;

public sealed record StartGeneralRevisionRequest(IReadOnlyList<Guid> RpuIds, int RevisionYear);

public sealed record GeneralRevisionJobDto(
    Guid Id,
    int RevisionYear,
    JobExecutionStatus Status,
    int TotalCount,
    int ProcessedCount,
    int FailedCount,
    Guid? StartedBy,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    string? Remarks);
