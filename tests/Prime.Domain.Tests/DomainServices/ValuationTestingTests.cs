using Prime.Domain.DomainServices;
using Shouldly;
using Xunit;

namespace Prime.Domain.Tests.DomainServices;

/// <summary>Step L6-3 (docs/analysis/smv-preparation-general-revision.md §4.3): value-to-price ratios, median and CoD. DEMO figures.</summary>
public class ValuationTestingTests
{
    [Theory]
    [InlineData(750, 1000, 0.75)]
    [InlineData(1, 3, 0.3333)]
    [InlineData(2, 3, 0.6667)]
    [InlineData(0, 500, 0)]
    public void Ratio_IsValueOverPrice_ToFourDecimals(decimal value, decimal price, decimal expected) =>
        ValuationTesting.Ratio(value, price).ShouldBe(expected);

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Ratio_OfANonPositivePrice_IsNull(decimal price) => ValuationTesting.Ratio(100m, price).ShouldBeNull();

    [Fact]
    public void Statistics_OddCount_MiddleRatio_AndAverageDeviationOverMedian()
    {
        // Deviations from 1.0: 0.2, 0.1, 0, 0.1, 0.2 → 0.12 on average → 12%.
        ValuationTesting.Statistics([1.2m, 0.8m, 1.0m, 1.1m, 0.9m]).ShouldBe((5, (decimal?)1.0m, (decimal?)12.00m));
    }

    [Fact]
    public void Statistics_EvenCount_MeanOfTheTwoMiddleRatios()
    {
        // Median 1.05; deviations 0.15, 0.05, 0.05, 0.25 → 0.125 on average → 11.904…%.
        ValuationTesting.Statistics([0.9m, 1.0m, 1.1m, 1.3m]).ShouldBe((4, (decimal?)1.05m, (decimal?)11.90m));
    }

    [Fact]
    public void Statistics_OfOneSale_HasNoDispersion() => ValuationTesting.Statistics([0.85m]).ShouldBe((1, (decimal?)0.85m, (decimal?)0m));

    [Fact]
    public void Statistics_OfNoSales_IsEmpty() => ValuationTesting.Statistics([]).ShouldBe((0, (decimal?)null, (decimal?)null));

    [Fact]
    public void Statistics_ZeroMedian_HasNoCoefficient() => ValuationTesting.Statistics([0m, 0m, 1m]).ShouldBe((3, (decimal?)0m, (decimal?)null));
}
