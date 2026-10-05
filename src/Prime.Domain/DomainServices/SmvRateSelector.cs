using Prime.Domain.Entities;

namespace Prime.Domain.DomainServices;

/// <summary>What a row is priced by, beyond its classification and property type.</summary>
public sealed record SmvRateKey(Guid? SubClassificationId, Guid? ZoneId, Guid? BarangayId, Guid? ActualUseId);

/// <summary>
/// Picks the unit value for a row from the rates in force (docs/analysis/valuation-foundation.md
/// §4.3, Q5). The SMV comes first: the latest-effective SMV that has a matching rate, itself or through
/// an amendment, so a new SMV supersedes an earlier one from its effectivity. An amendment's rows take
/// the place of the amended SMV's rows with the same key; the latest amendment wins
/// (smv-preparation-general-revision.md §4.7, Q17). Within the SMV so amended, the most specific rate:
/// sub-class + zone, sub-class + barangay, sub-class, then without sub-class: zone, barangay,
/// neither. At each level a rate naming the row's actual use beats one that does not.
/// A rate's optional parts that are set must equal the row's.
/// </summary>
public static class SmvRateSelector
{
    /// <param name="candidates">
    /// Approved rates in force for the row's classification and property type, with <see cref="SmvSchedule.Smv"/> loaded
    /// and, for an amendment's rate, <see cref="Smv.AmendsSmv"/>.
    /// </param>
    /// <param name="preferredSmvId">A proposed SMV being simulated: its rows win over the other members of its family.</param>
    public static SmvSchedule? Select(IEnumerable<SmvSchedule> candidates, SmvRateKey key, Guid? preferredSmvId = null)
    {
        var matching = candidates.Where(s => Matches(s, key)).ToList();
        if (matching.Count == 0)
        {
            return null;
        }
        var familyId = matching
            .OrderByDescending(s => Base(s).EffectivityDate).ThenByDescending(s => Base(s).ApprovedAt)
            .First().Smv!.FamilyId;
        return matching.Where(s => s.Smv!.FamilyId == familyId)
            .GroupBy(RowKey)
            .Select(g => g.OrderByDescending(s => s.SmvId == preferredSmvId)
                .ThenByDescending(s => s.Smv!.EffectivityDate).ThenByDescending(s => s.Smv!.ApprovedAt)
                .ThenByDescending(s => s.EffectiveDate).ThenByDescending(s => s.ApprovedAt)
                .First())
            .OrderBy(Specificity)
            .ThenByDescending(s => s.ActualUseId is not null)
            .ThenByDescending(s => s.EffectiveDate).ThenByDescending(s => s.ApprovedAt)
            .First();
    }

    public static bool Matches(SmvSchedule s, SmvRateKey key) =>
        (s.SubClassificationId is null || s.SubClassificationId == key.SubClassificationId)
        && (s.ZoneId is null || s.ZoneId == key.ZoneId)
        && (s.BarangayId is null || s.BarangayId == key.BarangayId)
        && (s.ActualUseId is null || s.ActualUseId == key.ActualUseId);

    /// <summary>0 = most specific (sub-class + zone) … 5 = least (no sub-class, zone or barangay).</summary>
    public static int Specificity(SmvSchedule s) =>
        (s.SubClassificationId is null ? 3 : 0) + (s.ZoneId is not null ? 0 : s.BarangayId is not null ? 1 : 2);

    /// <summary>The key an amendment's row replaces: every part that says what the row prices.</summary>
    public static (Guid, Guid, Guid?, Guid?, Guid?, Guid?, Guid?) RowKey(SmvSchedule s) =>
        (s.ClassificationId, s.PropertyTypeId, s.ImprovementKindId, s.SubClassificationId, s.ZoneId, s.BarangayId, s.ActualUseId);

    private static Smv Base(SmvSchedule s) =>
        s.Smv!.AmendsSmvId is null ? s.Smv : s.Smv.AmendsSmv ?? throw new InvalidOperationException("An amendment's rate needs its amended SMV loaded.");
}
