using System.Text.Json;
using Prime.Domain.DomainServices;
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
    DateTimeOffset ComputedAt,
    IReadOnlyList<ValuationLineDto>? Lines = null,
    string? SmvOrdinanceNumber = null,
    int? SmvRevisionYear = null);

/// <summary>One appraisal row of a valuation, with its own calculation (docs/analysis/value-and-assess.md §2).</summary>
public sealed record ValuationLineDto(
    int Sequence, ValuationLineSource Source, Guid? SourceId, string? Description,
    string? ClassificationName, string? SubClassificationName, string? ActualUseName,
    decimal? Quantity, string? Unit, decimal? UnitValue, Guid? SmvScheduleId, decimal MarketValue,
    IReadOnlyList<ValuationBreakdownItemDto> Breakdown);

public sealed record ValuationBreakdownItemDto(string Key, decimal Value);

/// <summary>A stored breakdown in calculation order: inputs, then intermediate values, then the market value.</summary>
public static class ValuationBreakdown
{
    private static readonly Dictionary<string, int> Rank =
        ValuationCalculator.BreakdownOrder.Select((key, i) => (key, i)).ToDictionary(x => x.key, x => x.i);

    public static IReadOnlyList<ValuationBreakdownItemDto> Ordered(string json) =>
        (JsonSerializer.Deserialize<Dictionary<string, decimal>>(json) ?? [])
        .OrderBy(kv => kv.Key == "MarketValue" ? int.MaxValue : Rank.GetValueOrDefault(kv.Key, int.MaxValue - 1))
        .ThenBy(kv => kv.Key, StringComparer.Ordinal)
        .Select(kv => new ValuationBreakdownItemDto(kv.Key, kv.Value)).ToList();
}
