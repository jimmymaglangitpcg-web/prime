using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using Prime.Application.Common.Interfaces;

namespace Prime.Infrastructure.Persistence;

/// <summary>
/// <see cref="ICollectionLock"/> as PostgreSQL transaction-level advisory
/// locks — one per (unit, tax year), keyed by a 64-bit hash of the pair. An
/// advisory lock needs no row to exist (e.g. before the first bill) and is
/// released automatically at commit or rollback. A hash collision only makes
/// two unrelated keys wait for each other, never lets two holders in.
/// </summary>
public sealed class CollectionLock(PrimeDbContext db) : ICollectionLock
{
    public async Task LockAsync(IEnumerable<(Guid RpuId, int TaxYear)> keys, CancellationToken cancellationToken = default)
    {
        var transaction = db.Database.CurrentTransaction
            ?? throw new InvalidOperationException("A collection lock needs an open transaction.");

        foreach (var (rpuId, taxYear) in keys.Distinct().OrderBy(k => k.RpuId).ThenBy(k => k.TaxYear))
        {
            await using var command = db.Database.GetDbConnection().CreateCommand();
            command.Transaction = transaction.GetDbTransaction();
            command.CommandText = "SELECT pg_advisory_xact_lock(hashtextextended(@key, 0))";
            command.Parameters.Add(new NpgsqlParameter("key", $"collection:{rpuId:N}:{taxYear}"));
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }
}
