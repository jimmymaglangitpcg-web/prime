namespace Prime.Domain.DomainServices;

/// <summary>
/// The money a payment actually leaves in the cash drawer per mode of payment
/// (docs/analysis/collection.md §4.7): tenders less the change given, the
/// change taken from the modes that allow it (cash), in tender order. Used for
/// remittance totals and the collection summary by mode.
/// </summary>
public static class TenderNetting
{
    public sealed record Tender(Guid PaymentModeId, decimal Amount, bool AllowsChange);

    /// <summary>Net amount per mode, in first-seen order. Throws if the change cannot come from change-giving modes.</summary>
    public static IReadOnlyList<(Guid PaymentModeId, decimal Amount)> Net(IReadOnlyList<Tender> tenders, decimal change)
    {
        if (change < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(change), "Change cannot be negative.");
        }
        var net = tenders.Select(t => (t.PaymentModeId, t.Amount, t.AllowsChange)).ToList();
        var remaining = change;
        for (var i = 0; i < net.Count && remaining > 0; i++)
        {
            if (!net[i].AllowsChange)
            {
                continue;
            }
            var taken = Math.Min(remaining, net[i].Amount);
            net[i] = (net[i].PaymentModeId, net[i].Amount - taken, true);
            remaining -= taken;
        }
        if (remaining > 0)
        {
            throw new InvalidOperationException("The change exceeds what change-giving modes of payment tendered.");
        }
        return net.GroupBy(t => t.PaymentModeId).Select(g => (g.Key, g.Sum(t => t.Amount))).ToList();
    }
}
