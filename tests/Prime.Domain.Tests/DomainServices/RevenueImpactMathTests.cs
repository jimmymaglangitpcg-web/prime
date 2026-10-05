using Prime.Domain.DomainServices;
using Shouldly;
using Xunit;

namespace Prime.Domain.Tests.DomainServices;

/// <summary>Step L6-5 (docs/analysis/smv-preparation-general-revision.md §4.5): tax gap and tax impact. DEMO figures, not rates of any LGU.</summary>
public class RevenueImpactMathTests
{
    [Fact]
    public void Compliance_TaxGapApproach()
    {
        // Potential 10,000,000 × 2% = 200,000; collection 150,000 + discounts 10,000 = 160,000; gap 40,000.
        var c = RevenueImpactMath.Compliance(10_000_000m, 2m, 150_000m, 10_000m);
        (c.TaxPotential, c.TotalCollection, c.TaxGap, c.ComplianceRatePercent, c.CollectionEfficiencyPercent)
            .ShouldBe((200_000m, 160_000m, 40_000m, (decimal?)80m, (decimal?)75m));
    }

    [Fact]
    public void Compliance_WithoutPotential_HasNoRates()
    {
        var c = RevenueImpactMath.Compliance(0m, 2m, 0m, 0m);
        (c.ComplianceRatePercent, c.CollectionEfficiencyPercent).ShouldBe(((decimal?)null, (decimal?)null));
    }

    [Theory]
    [InlineData(100_000, 2, 2_000)]
    [InlineData(33_333.33, 1.5, 500)]
    [InlineData(0, 2, 0)]
    public void Tax_IsAssessedValueTimesRate_ToCentavos(decimal av, decimal rate, decimal expected) => RevenueImpactMath.Tax(av, rate).ShouldBe(expected);

    [Fact]
    public void Summarize_LowerHigherUnchanged_RangeOfIncreases_AndReclassified()
    {
        UnitTax[] units =
        [
            new(1_000m, 800m, false),   // lower
            new(1_000m, 1_000m, false), // unchanged
            new(1_000m, 1_500m, false), // +500 (50%)
            new(2_000m, 2_100m, true),  // +100 (5%), reclassified
            new(0m, 300m, false),       // +300, no percent from zero
        ];
        var s = RevenueImpactMath.Summarize(units);
        (s.Units, s.CurrentTax, s.ScenarioTax, s.Lower, s.Higher, s.Unchanged).ShouldBe((5, 5_000m, 5_700m, 1, 3, 1));
        (s.SmallestIncrease, s.MedianIncrease, s.LargestIncrease, s.LargestIncreasePercent).ShouldBe(((decimal?)100m, (decimal?)300m, (decimal?)500m, (decimal?)50m));
        (s.Reclassified, s.ReclassifiedTaxChange).ShouldBe((1, 100m));
    }

    [Fact]
    public void Summarize_EvenIncreases_MedianIsTheMeanOfTheMiddleTwo() =>
        RevenueImpactMath.Summarize([new(0m, 100m, false), new(0m, 300m, false)]).MedianIncrease.ShouldBe(200m);
}
