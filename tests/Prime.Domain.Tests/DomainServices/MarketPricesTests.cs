using Prime.Domain.DomainServices;
using Shouldly;
using Xunit;

namespace Prime.Domain.Tests.DomainServices;

/// <summary>Step L6-1 (docs/analysis/smv-preparation-general-revision.md §4.1): unit prices and the sales spread. DEMO amounts.</summary>
public class MarketPricesTests
{
    [Fact]
    public void LandAlone_ConsiderationOverArea_RoundedToCentavos()
    {
        var (land, building) = MarketPrices.Compute(true, false, 1_000_000m, null, 300m, null);
        land.ShouldBe(3333.33m);
        building.ShouldBeNull();
    }

    [Fact]
    public void LandAndBuilding_WithoutTheLandPart_NeitherPriceIsToldApart()
    {
        var (land, building) = MarketPrices.Compute(true, true, 2_000_000m, null, 200m, 80m);
        land.ShouldBeNull();
        building.ShouldBeNull();
    }

    [Fact]
    public void LandAndBuilding_WithTheLandPart_SplitsTheConsideration()
    {
        var (land, building) = MarketPrices.Compute(true, true, 2_000_000m, 1_200_000m, 200m, 80m);
        land.ShouldBe(6000m);
        building.ShouldBe(10000m);
    }

    [Fact]
    public void BuildingAlone_ConsiderationOverFloorArea()
    {
        var (land, building) = MarketPrices.Compute(false, true, 450_000m, null, null, 60m);
        land.ShouldBeNull();
        building.ShouldBe(7500m);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void NoArea_NoPrice(int area)
    {
        MarketPrices.Compute(true, false, 100m, null, area, null).LandUnitPrice.ShouldBeNull();
    }

    [Fact]
    public void Spread_OddCount_MiddleValue()
    {
        MarketPrices.Spread([300m, 100m, 200m]).ShouldBe((100m, 200m, 300m));
    }

    [Fact]
    public void Spread_EvenCount_MeanOfTheTwoMiddle()
    {
        MarketPrices.Spread([100m, 401m, 200m, 300m]).ShouldBe((100m, 250m, 401m));
        MarketPrices.Spread([1m, 2m]).ShouldBe((1m, 1.5m, 2m));
    }

    [Fact]
    public void Spread_Empty_IsNull() => MarketPrices.Spread([]).ShouldBeNull();
}
