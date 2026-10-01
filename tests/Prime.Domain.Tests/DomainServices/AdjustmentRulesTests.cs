using Prime.Domain.DomainServices;
using Prime.Domain.Entities;
using Prime.Domain.Enums;
using Shouldly;
using Xunit;

namespace Prime.Domain.Tests.DomainServices;

/// <summary>The factor rule kinds and rounding of docs/analysis/valuation-foundation.md §4.4, with DEMO percentages.</summary>
public class AdjustmentRulesTests
{
    private static readonly Guid Paved = Guid.NewGuid(), Dirt = Guid.NewGuid();
    private static readonly LandFacts Land = new(Dirt, IsCornerLot: true, DistanceToAllWeatherRoadKm: 1m, DistanceToPoblacionKm: 3m, IsSubdivisionLot: false);

    private static AdjustmentFactor Factor(AdjustmentRuleKind kind, decimal percent = 0m, DistanceReference? reference = null, params AdjustmentFactorRow[] rows) =>
        new() { Code = "DEMO", RuleKind = kind, Percent = percent, DistanceReference = reference, Rows = rows.ToList() };

    [Fact]
    public void Flat_and_corner_use_the_factor_percent()
    {
        AdjustmentRules.Evaluate(Factor(AdjustmentRuleKind.Flat, -7m), Land, null).ShouldBe(AdjustmentOutcome.Of(-7m));
        AdjustmentRules.Evaluate(Factor(AdjustmentRuleKind.Corner, 15m), Land, null).ShouldBe(AdjustmentOutcome.Of(15m));
        AdjustmentRules.Evaluate(Factor(AdjustmentRuleKind.Corner, 15m), Land with { IsCornerLot = false }, null).Problem!.ShouldContain("corner lot");
    }

    [Fact]
    public void By_road_type_reads_the_land_road()
    {
        var factor = Factor(AdjustmentRuleKind.ByRoadType, rows: [new() { RoadTypeId = Paved, Percent = 0m }, new() { RoadTypeId = Dirt, Percent = -10m }]);
        AdjustmentRules.Evaluate(factor, Land, null).Percent.ShouldBe(-10m);
        AdjustmentRules.Evaluate(factor, Land with { RoadTypeId = null }, null).Problem!.ShouldContain("road type");
        AdjustmentRules.Evaluate(factor, Land with { RoadTypeId = Guid.NewGuid() }, null).Problem!.ShouldContain("no percentage");
    }

    [Theory]
    [InlineData(0, 0)]       // from zero, inclusive
    [InlineData(2, 0)]       // not over 2
    [InlineData(2.001, -5)]  // over 2
    [InlineData(5, -5)]
    [InlineData(50, -10)]    // no upper limit
    public void By_distance_reads_over_the_lower_not_over_the_upper(decimal km, decimal expected)
    {
        var factor = Factor(AdjustmentRuleKind.ByDistance, reference: DistanceReference.Poblacion, rows:
            [new() { UpToValue = 2m, Percent = 0m }, new() { OverValue = 2m, UpToValue = 5m, Percent = -5m }, new() { OverValue = 5m, Percent = -10m }]);
        AdjustmentRules.Evaluate(factor, Land with { DistanceToPoblacionKm = km }, null).Percent.ShouldBe(expected);
    }

    [Fact]
    public void By_distance_needs_the_distance_it_measures()
    {
        var factor = Factor(AdjustmentRuleKind.ByDistance, reference: DistanceReference.AllWeatherRoad, rows: [new() { Percent = -1m }]);
        AdjustmentRules.Evaluate(factor, Land with { DistanceToAllWeatherRoadKm = null }, null).Problem!.ShouldContain("all-weather road");
    }

    [Fact]
    public void Depth_prices_banded_strips_and_never_subdivision_lots()
    {
        var factor = Factor(AdjustmentRuleKind.Depth, rows: [new() { DepthBand = 1, Percent = -20m }, new() { DepthBand = 2, Percent = -40m }]);
        AdjustmentRules.Evaluate(factor, Land, 2).Percent.ShouldBe(-40m);
        AdjustmentRules.Evaluate(factor, Land, null).ShouldBe(AdjustmentOutcome.Skip()); // the standard strip
        AdjustmentRules.Evaluate(factor, Land, 3).Problem!.ShouldContain("band 3");
        AdjustmentRules.Evaluate(factor, Land with { IsSubdivisionLot = true }, 1).Problem!.ShouldContain("subdivision");
    }

    [Theory]
    [InlineData(null, 2.0, 2.0, 5.0, false)]
    [InlineData(null, 2.0, 1.5, 5.0, true)]
    [InlineData(5.0, null, 2.0, 6.0, true)]
    public void Bands_overlap_when_they_share_a_value(double? over1, double? upTo1, double? over2, double? upTo2, bool overlap) =>
        AdjustmentRules.BandsOverlap((decimal?)over1, (decimal?)upTo1, (decimal?)over2, (decimal?)upTo2).ShouldBe(overlap);

    [Theory]
    [InlineData(1234.5, 10, 1230)]
    [InlineData(1235, 10, 1240)]   // half away from zero
    [InlineData(1235, null, 1235)] // no step: unchanged
    public void Rounding_to_the_configured_step(decimal value, int? step, decimal expected)
    {
        var result = ValuationCalculator.WithRounding(
            new ValuationCalculationResult(ValuationMethod.SmvBased, value, new Dictionary<string, decimal> { ["MarketValue"] = value }), step);
        result.MarketValue.ShouldBe(expected);
        result.Breakdown["MarketValue"].ShouldBe(expected);
        if (step is not null)
        {
            (result.Breakdown["MarketValueBeforeRounding"], result.Breakdown["RoundingStep"]).ShouldBe((value, (decimal)step));
        }
    }
}
