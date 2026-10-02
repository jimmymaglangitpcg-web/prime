using Prime.Domain.Enums;

namespace Prime.Application.Features.RealPropertyUnits;

public sealed record CreateRpuRequest(
    Guid PropertyId,
    string RpuNumber,
    RpuType RpuType,
    DateOnly EffectivityDate,
    Guid? PreviousRpuId,
    Guid? LandRpuId = null,
    Guid? HostRpuId = null,
    bool IsLeasingProperty = false,
    string? FloorPrefix = null,
    int? FloorNumber = null,
    int? UnitNumber = null);

public sealed record RpuDto(
    Guid Id,
    Guid PropertyId,
    string RpuNumber,
    RpuType RpuType,
    RecordStatus Status,
    DateOnly EffectivityDate,
    DateOnly? EndDate,
    Guid? PreviousRpuId,
    DateTimeOffset CreatedAt,
    int? PinSuffix,
    string UnitPin,
    bool OwnedSeparately,
    Guid? LandRpuId,
    Guid? HostRpuId,
    bool IsLeasingProperty = false,
    string? FloorPrefix = null,
    int? FloorNumber = null,
    int? UnitNumber = null);
