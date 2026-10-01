using Prime.Domain.DomainServices;
using Prime.Domain.Entities;
using Shouldly;
using Xunit;

namespace Prime.Domain.Tests.DomainServices;

/// <summary>
/// The rate resolution order of docs/analysis/valuation-foundation.md §4.3 (Q5), with DEMO
/// unit values: the latest SMV first, then sub-class + zone → sub-class + barangay → sub-class
/// → zone → barangay → none; a rate naming the actual use beats one that does not.
/// </summary>
public class SmvRateSelectorTests
{
    private static readonly Guid Sub = Guid.NewGuid(), OtherSub = Guid.NewGuid(), Zone = Guid.NewGuid(), Barangay = Guid.NewGuid(), Use = Guid.NewGuid();
    private static readonly Smv Old = new() { EffectivityDate = new DateOnly(2020, 1, 1) };
    private static readonly Smv New = new() { EffectivityDate = new DateOnly(2027, 1, 1) };
    private static readonly SmvRateKey Key = new(Sub, Zone, Barangay, Use);

    private static SmvSchedule Rate(decimal value, Guid? sub = null, Guid? zone = null, Guid? barangay = null, Guid? use = null, Smv? smv = null) =>
        new() { MarketValue = value, SubClassificationId = sub, ZoneId = zone, BarangayId = barangay, ActualUseId = use, Smv = smv ?? Old, SmvId = (smv ?? Old).Id, EffectiveDate = (smv ?? Old).EffectivityDate };

    [Fact]
    public void The_most_specific_rate_wins_in_the_documented_order()
    {
        var rates = new List<SmvSchedule>
        {
            Rate(1), Rate(2, barangay: Barangay), Rate(3, zone: Zone), Rate(4, sub: Sub), Rate(5, sub: Sub, barangay: Barangay), Rate(6, sub: Sub, zone: Zone),
        };
        // Remove the winner each time: the next one in the order takes over.
        foreach (var expected in new decimal[] { 6, 5, 4, 3, 2, 1 })
        {
            var chosen = SmvRateSelector.Select(rates, Key)!;
            chosen.MarketValue.ShouldBe(expected);
            rates.Remove(chosen);
        }
        SmvRateSelector.Select(rates, Key).ShouldBeNull();
    }

    [Fact]
    public void A_rate_naming_the_actual_use_beats_one_that_does_not_at_the_same_level() =>
        SmvRateSelector.Select([Rate(1, sub: Sub), Rate(2, sub: Sub, use: Use)], Key)!.MarketValue.ShouldBe(2);

    [Fact]
    public void Set_parts_of_a_rate_must_equal_the_row()
    {
        SmvRateSelector.Select([Rate(1, sub: OtherSub), Rate(2, use: Guid.NewGuid())], Key).ShouldBeNull();
        // A row without a zone is not priced by a zone rate.
        SmvRateSelector.Select([Rate(1, zone: Zone), Rate(2)], Key with { ZoneId = null })!.MarketValue.ShouldBe(2);
    }

    [Fact]
    public void A_later_SMV_supersedes_even_a_more_specific_rate_of_an_earlier_one() =>
        SmvRateSelector.Select([Rate(1, sub: Sub, zone: Zone), Rate(2, smv: New)], Key)!.MarketValue.ShouldBe(2);

    [Fact]
    public void An_SMV_without_a_matching_rate_does_not_hide_an_earlier_one() =>
        SmvRateSelector.Select([Rate(1), Rate(2, sub: OtherSub, smv: New)], Key)!.MarketValue.ShouldBe(1);
}
