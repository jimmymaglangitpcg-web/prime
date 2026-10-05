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

    // --- Amendments (smv-preparation-general-revision.md §4.7, Q17) ---

    private static Smv Amendment(Smv amended, int year) =>
        new() { Basis = Prime.Domain.Enums.SmvBasis.Amendment, AmendsSmvId = amended.Id, AmendsSmv = amended, EffectivityDate = new DateOnly(year, 1, 1) };

    [Fact]
    public void An_amendment_row_replaces_the_amended_row_with_the_same_key()
    {
        var amendment = Amendment(Old, 2022);
        SmvRateSelector.Select([Rate(1, barangay: Barangay), Rate(2), Rate(3, barangay: Barangay, smv: amendment)], Key)!.MarketValue.ShouldBe(3);
    }

    [Fact]
    public void A_less_specific_amendment_row_does_not_hide_a_more_specific_amended_row()
    {
        // The amendment changes the barangay value; the zone value of the amended SMV still applies to a row in the zone.
        var amendment = Amendment(Old, 2022);
        SmvRateSelector.Select([Rate(1, zone: Zone), Rate(2, barangay: Barangay), Rate(3, barangay: Barangay, smv: amendment)], Key)!.MarketValue.ShouldBe(1);
        SmvRateSelector.Select([Rate(1, zone: Zone), Rate(2, barangay: Barangay), Rate(3, barangay: Barangay, smv: amendment)], Key with { ZoneId = null })!
            .MarketValue.ShouldBe(3);
    }

    [Fact]
    public void An_amendment_may_add_a_row_the_amended_SMV_lacks() =>
        SmvRateSelector.Select([Rate(1), Rate(2, zone: Zone, smv: Amendment(Old, 2022))], Key)!.MarketValue.ShouldBe(2);

    [Fact]
    public void The_latest_amendment_wins_for_the_same_key()
    {
        var first = Amendment(Old, 2022);
        var second = Amendment(Old, 2023);
        SmvRateSelector.Select([Rate(1), Rate(3, smv: second), Rate(2, smv: first)], Key)!.MarketValue.ShouldBe(3);
    }

    [Fact]
    public void An_amendment_of_an_earlier_SMV_does_not_beat_a_later_SMV()
    {
        // The amendment is newer than the later SMV, but it amends the earlier one: the later SMV's family is chosen.
        var amendment = Amendment(Old, 2028);
        SmvRateSelector.Select([Rate(1, smv: New), Rate(2, sub: Sub, zone: Zone, smv: amendment)], Key)!.MarketValue.ShouldBe(1);
    }

    [Fact]
    public void A_preferred_proposed_amendment_wins_over_its_family()
    {
        var approved = Amendment(Old, 2024);
        var proposed = Amendment(Old, 2023);
        SmvRateSelector.Select([Rate(1), Rate(2, smv: approved), Rate(3, smv: proposed)], Key, proposed.Id)!.MarketValue.ShouldBe(3);
    }
}
