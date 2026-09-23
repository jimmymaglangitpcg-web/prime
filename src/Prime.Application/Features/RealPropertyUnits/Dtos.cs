using Prime.Domain.Enums;

namespace Prime.Application.Features.RealPropertyUnits;

public sealed record CreateRpuRequest(
    Guid PropertyId,
    string RpuNumber,
    RpuType RpuType,
    DateOnly EffectivityDate,
    Guid? PreviousRpuId);

public sealed record RpuDto(
    Guid Id,
    Guid PropertyId,
    string RpuNumber,
    RpuType RpuType,
    RecordStatus Status,
    DateOnly EffectivityDate,
    DateOnly? EndDate,
    Guid? PreviousRpuId,
    DateTimeOffset CreatedAt);
