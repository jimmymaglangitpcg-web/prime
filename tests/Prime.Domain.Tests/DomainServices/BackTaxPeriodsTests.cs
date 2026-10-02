using Prime.Domain.DomainServices;
using Shouldly;
using Xunit;

namespace Prime.Domain.Tests.DomainServices;

/// <summary>Step L1-8 (docs/analysis/valuation-foundation.md §4.8): the back-tax start limit and the periods. DEMO dates.</summary>
public class BackTaxPeriodsTests
{
    [Theory]
    [InlineData(2016, 2025, 10, true)]   // 9 years
    [InlineData(2015, 2025, 10, true)]   // exactly 10
    [InlineData(2014, 2025, 10, false)]  // 11
    [InlineData(2025, 2025, 10, true)]   // the initial year itself
    [InlineData(2026, 2025, 10, false)]  // after it
    public void StartProblem_AllowsUpToTheLimitBeforeTheInitialYear(int from, int initial, int limit, bool allowed) =>
        (BackTaxPeriods.StartProblem(from, initial, limit) is null).ShouldBe(allowed);

    [Fact]
    public void Split_CutsAtEachSmvEffectivityAfterTheStart_TheLastStaysCurrent()
    {
        var periods = BackTaxPeriods.Split(2016, new DateOnly(2025, 12, 31),
            [new(2014, 1, 1), new(2017, 1, 1), new(2019, 7, 1), new(2019, 7, 1), new(2016, 1, 1), new(2026, 1, 1)]);

        periods.Select(p => (p.Sequence, p.Start, p.End)).ShouldBe([
            (1, new DateOnly(2016, 1, 1), (DateOnly?)new DateOnly(2016, 12, 31)),
            (2, new DateOnly(2017, 1, 1), new DateOnly(2019, 6, 30)),
            (3, new DateOnly(2019, 7, 1), null),  // 2026 is after the range; 2014 and 2016 not after the start
        ]);
    }

    [Fact]
    public void Split_WithoutCuts_IsOneCurrentPeriod() =>
        BackTaxPeriods.Split(2020, new DateOnly(2025, 12, 31), []).Single().ShouldBe(new BackTaxPeriodSpan(1, new DateOnly(2020, 1, 1), null));
}
