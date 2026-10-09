using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Prime.Application.Common.Interfaces;
using Prime.Domain.Common;

namespace Prime.Infrastructure.Persistence.Concurrency;

/// <summary>
/// The request's <see cref="IConcurrencyExpectation"/>; scoped, set by the WebApi's If-Match filter and applied by
/// <see cref="ConcurrencyMaterializationInterceptor"/> and <see cref="ConcurrencyExpectationInterceptor"/>
/// (docs/analysis/production-hardening.md §4.4).
/// </summary>
public sealed class ConcurrencyExpectation : IConcurrencyExpectation
{
    public const string ConflictMessage =
        "This record was changed by someone else since it was loaded. Nothing was saved; reload it and try again.";

    public Guid? SubjectId { get; private set; }
    public uint? ExpectedVersion { get; private set; }

    /// <summary>Checked once, the first time the request reads or saves the subject; later reads compare nothing.</summary>
    private bool _checked;

    public void Expect(Guid subjectId, uint version)
    {
        SubjectId = subjectId;
        ExpectedVersion = version;
        _checked = false;
    }

    /// <summary>
    /// The subject as the request first sees it must be at the version the client displayed; otherwise the request
    /// stops there, before any rule is evaluated against a record the user has not seen, and nothing is saved.
    /// </summary>
    internal void Check(Guid id, uint version)
    {
        if (_checked || SubjectId != id || ExpectedVersion is not { } expected)
        {
            return;
        }
        _checked = true;
        if (version != expected)
        {
            throw new DbUpdateConcurrencyException(ConflictMessage);
        }
    }
}

/// <summary>
/// Checks the expectation the first time the request reads the subject from the database. Singleton (EF caches
/// materialization interceptors with its internal services); it reaches the request's expectation through the
/// <see cref="PrimeDbContext"/>.
/// </summary>
public sealed class ConcurrencyMaterializationInterceptor : IMaterializationInterceptor
{
    public static readonly ConcurrencyMaterializationInterceptor Instance = new();

    private ConcurrencyMaterializationInterceptor()
    {
    }

    public object InitializedInstance(MaterializationInterceptionData materializationData, object entity)
    {
        if (entity is IVersioned versioned and Entity loaded && materializationData.Context is PrimeDbContext { Concurrency: { } expectation })
        {
            expectation.Check(loaded.Id, versioned.RowVersion);
        }
        return entity;
    }
}

/// <summary>
/// Checks the expectation at a save, for a subject the request saves without having read it first (it was attached).
/// From the read or save on, the row version's "WHERE xmin = @loaded" covers the time between load and save.
/// </summary>
public sealed class ConcurrencyExpectationInterceptor(ConcurrencyExpectation expectation) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Check(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Check(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Check(DbContext? context)
    {
        if (context is null || expectation.SubjectId is not { } id)
        {
            return;
        }
        var entry = context.ChangeTracker.Entries<IVersioned>()
            .FirstOrDefault(e => e.State != EntityState.Added && e.Entity is Entity entity && entity.Id == id);
        if (entry is not null)
        {
            expectation.Check(id, entry.Property(x => x.RowVersion).OriginalValue);
        }
    }
}
