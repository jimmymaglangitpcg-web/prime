using Prime.Domain.DomainServices;
using Prime.Domain.Enums;
using Shouldly;
using Xunit;

namespace Prime.Domain.Tests.DomainServices;

/// <summary>The rule boundaries of docs/analysis/valuation-foundation.md §4.2 (31 Dec / 1 Jan, quarter ends).</summary>
public class EffectivityRulesTests
{
    [Theory]
    [InlineData("2026-01-01", "2026-01-01")] // made on 1 January: that day
    [InlineData("2026-01-02", "2027-01-01")]
    [InlineData("2026-06-30", "2027-01-01")]
    [InlineData("2026-12-31", "2027-01-01")]
    [InlineData("2028-02-29", "2029-01-01")]
    public void NextJanuary(string madeOn, string expected) =>
        EffectivityRules.Derive(EffectivityRule.NextJanuary, DateOnly.Parse(madeOn)).ShouldBe(DateOnly.Parse(expected));

    [Theory]
    [InlineData("2026-01-01", "2026-04-01")] // the first day of a quarter still takes the next one
    [InlineData("2026-03-31", "2026-04-01")]
    [InlineData("2026-04-01", "2026-07-01")]
    [InlineData("2026-06-30", "2026-07-01")]
    [InlineData("2026-09-30", "2026-10-01")]
    [InlineData("2026-10-01", "2027-01-01")]
    [InlineData("2026-12-31", "2027-01-01")]
    public void NextQuarter(string madeOn, string expected) =>
        EffectivityRules.Derive(EffectivityRule.NextQuarter, DateOnly.Parse(madeOn)).ShouldBe(DateOnly.Parse(expected));

    [Theory]
    [InlineData(EffectivityRule.Periods)]
    [InlineData(EffectivityRule.Fixed)]
    public void ProcessGivenRules_DeriveNothing(EffectivityRule rule)
    {
        EffectivityRules.Derive(rule, new DateOnly(2026, 5, 5)).ShouldBeNull();
        EffectivityRules.IsDerived(rule).ShouldBeFalse();
    }

    [Theory]
    [InlineData("2026-01-01", 1)]
    [InlineData("2026-03-31", 1)]
    [InlineData("2026-04-01", 2)]
    [InlineData("2026-09-30", 3)]
    [InlineData("2026-12-31", 4)]
    public void Quarter(string date, int expected) => EffectivityRules.Quarter(DateOnly.Parse(date)).ShouldBe(expected);

    [Theory]
    [InlineData("2026-01-01", "2026-04-01", 90, false)] // day 90: in time
    [InlineData("2026-01-01", "2026-04-02", 90, true)]  // day 91: late
    [InlineData("2026-01-01", "2026-12-31", null, false)] // no window configured
    public void CauseWindow(string cause, string madeOn, int? days, bool late) =>
        EffectivityRules.CauseWindowExceeded(DateOnly.Parse(cause), DateOnly.Parse(madeOn), days).ShouldBe(late);
}
