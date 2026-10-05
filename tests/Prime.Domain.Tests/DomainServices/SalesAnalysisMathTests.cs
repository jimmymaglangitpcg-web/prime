using Prime.Domain.DomainServices;
using Shouldly;
using Xunit;

namespace Prime.Domain.Tests.DomainServices;

/// <summary>Step L6-2b (docs/analysis/smv-preparation-general-revision.md §4.2): rounding, intervals and ranges. DEMO unit values.</summary>
public class SalesAnalysisMathTests
{
    [Theory]
    [InlineData(2233, 100, 2200)]
    [InlineData(2250, 100, 2300)]
    [InlineData(2249.99, 100, 2200)]
    [InlineData(1234, 0, 1234)]
    public void RoundTo_NearestIncrement(decimal value, decimal increment, decimal expected) => SalesAnalysisMath.RoundTo(value, increment).ShouldBe(expected);

    [Fact]
    public void Adjust_TimeFactorThenOtherAdjustment() => SalesAnalysisMath.Adjust(1_000m, 1.05m, -10m).ShouldBe(945m);

    [Fact]
    public void Intervals_RiseOverThePrevious_AndTheirAverage()
    {
        decimal[] sorted = [2000m, 2200m, 2420m, 2420m];
        SalesAnalysisMath.Intervals(sorted).ShouldBe([null, 10m, 10m, 0m]);
        SalesAnalysisMath.AverageInterval(sorted).ShouldBe(6.67m);
        SalesAnalysisMath.AverageInterval([5000m]).ShouldBeNull();
    }

    [Fact]
    public void Ranges_StartAtTheLowestFreeValue_MidAndHighAtPlusW()
    {
        // ±6%: 2,000 → mid 2,100 (2,120 rounded), high 2,226; then 2,300 → mid 2,400 (2,438), high 2,544; then 3,000 → 3,200 (3,180), 3,392.
        decimal[] sorted = [2000m, 2100m, 2200m, 2300m, 2500m, 3000m, 3300m];
        var ranges = SalesAnalysisMath.Ranges(sorted, 6m, 100m);
        ranges.Select(r => (r.Number, r.Low, r.Mid, r.High, r.Frequency)).ShouldBe([
            (1, 2000m, 2100m, 2226m, 3), (2, 2300m, 2400m, 2544m, 2), (3, 3000m, 3200m, 3392m, 2)]);
        ranges.Sum(r => r.Frequency).ShouldBe(sorted.Length);
    }

    [Fact]
    public void Ranges_OfAWidthTooSmallForTheIncrement_StillTakeEachValue()
    {
        var ranges = SalesAnalysisMath.Ranges([1000m, 1000m, 5000m], 1m, 100m);
        ranges.Select(r => (r.Low, r.Mid, r.High, r.Frequency)).ShouldBe([(1000m, 1000m, 1010m, 2), (5000m, 5100m, 5151m, 1)]);
    }

    [Fact]
    public void Ranges_RefuseANonPositiveWidth() => Should.Throw<ArgumentOutOfRangeException>(() => SalesAnalysisMath.Ranges([1m], 0m, 100m));

    [Fact]
    public void ProposedValue_FrequencyWeightedMeanOfTheMergedMidpoints()
    {
        var ranges = SalesAnalysisMath.Ranges([2000m, 2100m, 2200m, 2300m, 2500m], 6m, 100m);
        // (2,100 × 3 + 2,400 × 2) / 5 = 2,220 → 2,200.
        SalesAnalysisMath.ProposedValue(ranges, 100m).ShouldBe(2200m);
    }
}
