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
    int? SmvRevisionYear = null,
    /// <summary>Set when valued under the rules of another date (a back-tax period at current rules; valuation-foundation.md §4.8).</summary>
    DateOnly? RulesAsOf = null);

/// <summary>One appraisal row of a valuation, with its own calculation (docs/analysis/value-and-assess.md §2).</summary>
public sealed record ValuationLineDto(
    int Sequence, ValuationLineSource Source, Guid? SourceId, string? Description,
    string? ClassificationName, string? SubClassificationName, string? ActualUseName,
    decimal? Quantity, string? Unit, decimal? UnitValue, Guid? SmvScheduleId, decimal MarketValue,
    IReadOnlyList<ValuationBreakdownItemDto> Breakdown,
    /// <summary>Set when the row was priced by another class and sub-class than the ones that assess it (valuation-foundation.md §4.3).</summary>
    string? PricedClassificationName = null, string? PricedSubClassificationName = null,
    /// <summary>The independent appraisal the row was valued by (valuation-foundation.md §4.7).</summary>
    Guid? IndependentAppraisalId = null,
    /// <summary>The ids the row is assessed under; null: the unit's Tax Declaration's.</summary>
    Guid? ClassificationId = null, Guid? ActualUseId = null);

public sealed record ValuationBreakdownItemDto(string Key, decimal Value);

/// <summary>A stored breakdown in calculation order: inputs, then intermediate values, then the market value.</summary>
public static class ValuationBreakdown
{
    private static readonly Dictionary<string, int> Rank =
        ValuationCalculator.BreakdownOrder.Select((key, i) => (key, i)).ToDictionary(x => x.key, x => x.i);

    public static IReadOnlyList<ValuationBreakdownItemDto> Ordered(string json) =>
        (JsonSerializer.Deserialize<Dictionary<string, decimal>>(json) ?? [])
        .OrderBy(kv => Position(kv.Key))
        .ThenBy(kv => kv.Key, StringComparer.Ordinal)
        .Select(kv => new ValuationBreakdownItemDto(kv.Key, kv.Value)).ToList();

    /// <summary>Listed keys in order; each extra item just before their total, each factor just before the total percent; others before MarketValue.</summary>
    private static long Position(string key) =>
        key == "MarketValue" ? long.MaxValue
        : key.StartsWith(ValuationCalculator.InputKeyPrefix, StringComparison.Ordinal) ? 2L * Rank["AppraisedValue"] - 1
        : key.StartsWith(ValuationCalculator.ExtraItemKeyPrefix, StringComparison.Ordinal) ? 2L * Rank["AdditionalItemsCost"] - 1
        : key.StartsWith(ValuationCalculator.AdjustmentKeyPrefix, StringComparison.Ordinal) ? 2L * Rank["AdjustmentPercent"] - 1
        : Rank.TryGetValue(key, out var r) ? 2L * r : long.MaxValue - 1;
}
