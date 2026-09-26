namespace Prime.Application.Common.Interfaces;

/// <summary>
/// Serializes changes to what is owed and paid for a unit and tax year
/// (docs/analysis/collection.md §3; CLAUDE.md §66): payment posting, bill
/// posting and bill cancellation take this lock before reading balances. The
/// lock is held until the caller's current transaction ends, so a transaction
/// must be open. Keys are locked in a fixed order, so two callers locking
/// overlapping sets cannot deadlock.
/// </summary>
public interface ICollectionLock
{
    Task LockAsync(IEnumerable<(Guid RpuId, int TaxYear)> keys, CancellationToken cancellationToken = default);
}
