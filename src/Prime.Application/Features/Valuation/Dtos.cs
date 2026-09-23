using Prime.Domain.Enums;

namespace Prime.Application.Features.Valuation;

public sealed record ValuationDto(
    Guid Id,
    Guid RpuId,
    Guid PropertyId,
    ValuationSourceType SourceType,
    Guid SourceId,
    Guid? SmvId,
    Guid? SmvScheduleId,
    ValuationMethod ValuationMethod,
    decimal ComputedMarketValue,
    IReadOnlyDictionary<string, decimal> Breakdown,
    DateOnly EffectiveDate,
    DateTimeOffset ComputedAt);
