using Prime.Domain.Entities;
using Prime.Domain.Enums;

namespace Prime.Domain.DomainServices;

/// <summary>The land's facts a factor rule reads (docs/analysis/valuation-foundation.md §4.4).</summary>
public sealed record LandFacts(Guid? RoadTypeId, bool IsCornerLot, decimal? DistanceToAllWeatherRoadKm, decimal? DistanceToPoblacionKm, bool IsSubdivisionLot);

/// <summary>
/// The percentage a factor gives a strip: <see cref="Applies"/> false when the rule does not
/// touch the strip (a depth factor on a strip without a band); <see cref="Problem"/> when the
/// factor was named but cannot apply, which stops the valuation with that message.
/// </summary>
public sealed record AdjustmentOutcome(bool Applies, decimal Percent, string? Problem)
{
    public static AdjustmentOutcome Of(decimal percent) => new(true, percent, null);
    public static AdjustmentOutcome Skip() => new(false, 0m, null);
    public static AdjustmentOutcome Refuse(string problem) => new(false, 0m, problem);
}

/// <summary>
/// The rule kinds of SMV adjustment factors, as pure functions of the factor and the land
/// (LAM Bk III pp.76–78). Distance bands read "over the lower, not over the upper", like the
/// assessment level brackets.
/// </summary>
public static class AdjustmentRules
{
    public static AdjustmentOutcome Evaluate(AdjustmentFactor factor, LandFacts land, int? stripDepthBand)
    {
        switch (factor.RuleKind)
        {
            case AdjustmentRuleKind.Flat:
                return AdjustmentOutcome.Of(factor.Percent);
            case AdjustmentRuleKind.Corner:
                return land.IsCornerLot
                    ? AdjustmentOutcome.Of(factor.Percent)
                    : AdjustmentOutcome.Refuse($"Factor {factor.Code} is for corner lots; the land is not recorded as a corner lot.");
            case AdjustmentRuleKind.ByRoadType:
                if (land.RoadTypeId is not { } road)
                {
                    return AdjustmentOutcome.Refuse($"Factor {factor.Code} depends on the kind of road; record the land's road type.");
                }
                return factor.Rows.FirstOrDefault(r => r.RoadTypeId == road) is { } byRoad
                    ? AdjustmentOutcome.Of(byRoad.Percent)
                    : AdjustmentOutcome.Refuse($"Factor {factor.Code} gives no percentage for the land's road type.");
            case AdjustmentRuleKind.ByDistance:
                var distance = factor.DistanceReference == DistanceReference.Poblacion ? land.DistanceToPoblacionKm : land.DistanceToAllWeatherRoadKm;
                var what = factor.DistanceReference == DistanceReference.Poblacion ? "the poblacion" : "an all-weather road";
                if (distance is not { } km)
                {
                    return AdjustmentOutcome.Refuse($"Factor {factor.Code} depends on the distance to {what}; record it on the land.");
                }
                return factor.Rows.FirstOrDefault(r => InBand(km, r.OverValue, r.UpToValue)) is { } band
                    ? AdjustmentOutcome.Of(band.Percent)
                    : AdjustmentOutcome.Refuse($"Factor {factor.Code} gives no percentage for {km:0.###} km to {what}.");
            case AdjustmentRuleKind.Depth:
                if (land.IsSubdivisionLot)
                {
                    return AdjustmentOutcome.Refuse($"Factor {factor.Code} is a depth adjustment, which does not apply to subdivision lots.");
                }
                if (stripDepthBand is not { } depthBand)
                {
                    return AdjustmentOutcome.Skip();
                }
                return factor.Rows.FirstOrDefault(r => r.DepthBand == depthBand) is { } depth
                    ? AdjustmentOutcome.Of(depth.Percent)
                    : AdjustmentOutcome.Refuse($"Factor {factor.Code} gives no percentage for depth band {depthBand}.");
            default:
                throw new ArgumentOutOfRangeException(nameof(factor), factor.RuleKind, null);
        }
    }

    /// <summary>Over <paramref name="over"/> (from zero inclusive when null), not over <paramref name="upTo"/> (unbounded when null).</summary>
    public static bool InBand(decimal value, decimal? over, decimal? upTo) =>
        (over is null ? value >= 0 : value > over) && (upTo is null || value <= upTo);

    /// <summary>Whether two bands share a value, read as in <see cref="InBand"/>.</summary>
    public static bool BandsOverlap(decimal? over1, decimal? upTo1, decimal? over2, decimal? upTo2)
    {
        var lower1 = over1 ?? -1m;
        var lower2 = over2 ?? -1m;
        return (upTo2 is null || lower1 < upTo2) && (upTo1 is null || lower2 < upTo1);
    }
}
