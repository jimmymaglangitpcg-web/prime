using Prime.Domain.Entities;

namespace Prime.Domain.DomainServices;

/// <summary>What a row is priced by, beyond its classification and property type.</summary>
public sealed record SmvRateKey(Guid? SubClassificationId, Guid? ZoneId, Guid? BarangayId, Guid? ActualUseId);

/// <summary>
/// Picks the unit value for a row from the rates in force (docs/analysis/valuation-foundation.md
/// §4.3, Q5). The SMV comes first: the latest-effective SMV that has a matching rate, so a new
/// SMV supersedes an earlier one from its effectivity. Within it, the most specific rate:
/// sub-class + zone, sub-class + barangay, sub-class, then without sub-class: zone, barangay,
/// neither. At each level a rate naming the row's actual use beats one that does not.
/// A rate's optional parts that are set must equal the row's.
/// </summary>
public static class SmvRateSelector
{
    /// <param name="candidates">Approved rates in force for the row's classification and property type, with <see cref="SmvSchedule.Smv"/> loaded.</param>
    public static SmvSchedule? Select(IEnumerable<SmvSchedule> candidates, SmvRateKey key)
    {
        var matching = candidates.Where(s => Matches(s, key)).ToList();
        if (matching.Count == 0)
        {
            return null;
        }
        var smvId = matching
            .OrderByDescending(s => s.Smv!.EffectivityDate).ThenByDescending(s => s.Smv!.ApprovedAt)
            .First().SmvId;
        return matching.Where(s => s.SmvId == smvId)
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
}
