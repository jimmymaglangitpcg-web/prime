using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Prime.Application.Common.Interfaces;
using Prime.Domain.Common;
using Prime.Domain.Entities.Audit;
using Prime.Domain.Enums;

namespace Prime.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Stamps CreatedAt/CreatedBy/UpdatedAt/UpdatedBy on every tracked
/// <see cref="IAuditable"/> entity, and writes one <see cref="AuditLog"/>
/// row per Added/Modified/Deleted IAuditable entity — in the SAME
/// SaveChanges batch (added to the change tracker here, before the base
/// interceptor lets the command execute), so auditing is atomic with the
/// change it describes and no feature handler can forget to call it
/// (CLAUDE.md §48, docs/DATABASE.md §6).
/// </summary>
public class AuditSaveChangesInterceptor(ICurrentUserService currentUser) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        ProcessAuditableEntries(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ProcessAuditableEntries(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void ProcessAuditableEntries(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var auditLogs = new List<AuditLog>();

        foreach (EntityEntry<IAuditable> entry in context.ChangeTracker.Entries<IAuditable>())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
            {
                continue;
            }

            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = now;
                    entry.Entity.CreatedBy ??= currentUser.AppUserId;
                    break;
                case EntityState.Modified:
                    entry.Entity.UpdatedAt = now;
                    entry.Entity.UpdatedBy = currentUser.AppUserId;
                    break;
            }

            auditLogs.Add(BuildAuditLog(entry, now));
        }

        foreach (var log in auditLogs)
        {
            context.Add(log);
        }
    }

    private AuditLog BuildAuditLog(EntityEntry entry, DateTimeOffset now)
    {
        var idProperty = entry.Property("Id");
        var recordId = idProperty.CurrentValue is Guid guid ? guid : Guid.Empty;

        var (action, oldValue, newValue) = entry.State switch
        {
            EntityState.Added => (AuditAction.Create, (string?)null, Serialize(entry.CurrentValues.Properties.Select(p => (p.Name, entry.CurrentValues[p])))),
            EntityState.Deleted => (AuditAction.Delete, Serialize(entry.OriginalValues.Properties.Select(p => (p.Name, entry.OriginalValues[p]))), (string?)null),
            _ => (AuditAction.Update, SerializeChangedOriginal(entry), SerializeChangedCurrent(entry)),
        };

        return new AuditLog
        {
            UserId = currentUser.AppUserId,
            Module = ResolveModule(entry.Metadata.ClrType.Namespace),
            TableName = entry.Metadata.GetTableName() ?? entry.Metadata.ClrType.Name,
            RecordId = recordId,
            Action = action,
            OldValue = oldValue,
            NewValue = newValue,
            Timestamp = now,
            IpAddress = currentUser.IpAddress,
            Reason = currentUser.Reason,
        };
    }

    private static string? SerializeChangedCurrent(EntityEntry entry)
    {
        var changed = entry.Properties.Where(p => p.IsModified).Select(p => (p.Metadata.Name, p.CurrentValue));
        return Serialize(changed);
    }

    private static string? SerializeChangedOriginal(EntityEntry entry)
    {
        var changed = entry.Properties.Where(p => p.IsModified).Select(p => (p.Metadata.Name, p.OriginalValue));
        return Serialize(changed);
    }

    private static string? Serialize(IEnumerable<(string Name, object? Value)> values)
    {
        // NetTopologySuite Geometry values (e.g. Parcel.Geometry) are not
        // JSON-serializable as-is — their internal coordinates can contain
        // NaN (e.g. an unset Z on a 2D point), which System.Text.Json
        // refuses to write, and jsonb has no NaN literal even if it did.
        // Captured as WKT text instead, which is both valid JSON and
        // human-readable in the audit trail.
        var dict = values.ToDictionary(v => v.Name, object? (v) => v.Value is NetTopologySuite.Geometries.Geometry geometry ? geometry.AsText() : v.Value);
        return dict.Count == 0 ? null : JsonSerializer.Serialize(dict);
    }

    /// <summary>
    /// Coarse module grouping derived from the entity's namespace segment
    /// (e.g. Prime.Domain.Entities.Identity → "Identity"). A simple,
    /// defensible heuristic — CLAUDE.md §48 does not define the exact
    /// module taxonomy, and this keeps AuditLog useful without inventing
    /// one prematurely.
    /// </summary>
    private static string ResolveModule(string? clrNamespace)
    {
        if (string.IsNullOrEmpty(clrNamespace))
        {
            return "Core";
        }

        var lastSegment = clrNamespace.Split('.').Last();
        return lastSegment == "Entities" ? "PropertyRegistry" : lastSegment;
    }
}
