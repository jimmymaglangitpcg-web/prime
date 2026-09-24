using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using Prime.Application.Common.Interfaces;
using Prime.Infrastructure.Persistence;

namespace Prime.Infrastructure.Documents;

/// <summary>
/// PostgreSQL upsert on (NumberingSchemeId, ScopeKey): the first number of a
/// scope is 1; afterwards the row is incremented and locked until the
/// caller's transaction ends, so concurrent issuers in the same scope queue
/// rather than collide, and a rolled-back issuance leaves no gap.
/// </summary>
public sealed class NumberSequenceAllocator(PrimeDbContext db) : INumberSequenceAllocator
{
    private const string Sql = """
        INSERT INTO "NumberSequences" ("Id", "NumberingSchemeId", "ScopeKey", "LastValue")
        VALUES (@id, @scheme, @scope, 1)
        ON CONFLICT ("NumberingSchemeId", "ScopeKey")
        DO UPDATE SET "LastValue" = "NumberSequences"."LastValue" + 1
        RETURNING "LastValue";
        """;

    public async Task<long> NextAsync(Guid numberingSchemeId, string scopeKey, CancellationToken cancellationToken = default)
    {
        var connection = db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await db.Database.OpenConnectionAsync(cancellationToken);
        }
        await using var command = connection.CreateCommand();
        command.CommandText = Sql;
        command.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
        command.Parameters.Add(new NpgsqlParameter("id", Guid.NewGuid()));
        command.Parameters.Add(new NpgsqlParameter("scheme", numberingSchemeId));
        command.Parameters.Add(new NpgsqlParameter("scope", scopeKey));
        return (long)(await command.ExecuteScalarAsync(cancellationToken))!;
    }
}
