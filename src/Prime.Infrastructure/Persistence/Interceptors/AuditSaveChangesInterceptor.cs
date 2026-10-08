using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata;
using Prime.Application.Common.Interfaces;
using Prime.Domain.Common;
using Prime.Domain.Entities.Audit;
using Prime.Domain.Enums;

namespace Prime.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Stamps CreatedAt/CreatedBy/UpdatedAt/UpdatedBy on every tracked
/// <see cref="IAuditable"/> entity, and writes one <see cref="AuditLog"/>
/// row per Added/Modified/Deleted entity — in the SAME SaveChanges batch
/// (added to the change tracker here, before the base interceptor lets the
/// command execute), so auditing is atomic with the change it describes and
/// no feature handler can forget to call it (CLAUDE.md §48, docs/DATABASE.md §6).
/// <para>
/// Every table is audited (docs/analysis/workflow-security.md §4.3, Q10): a
/// plain <see cref="Entity"/> child is logged with its parent's table and id.
/// The exclusions are <see cref="ExcludedTypes"/>. A change of a workflow
/// status is logged as the action it is (APPROVE, REJECT, POST, VOID, CANCEL,
/// REVERSE) instead of UPDATE.
/// </para>
/// </summary>
public class AuditSaveChangesInterceptor(ICurrentUserService currentUser) : SaveChangesInterceptor
{
    /// <summary>
    /// Not audited, each for a stated reason (docs/analysis/workflow-security.md §4.3):
    /// the audit log itself; results written in bulk by a job whose run is audited
    /// (simulation results and their rows, valuation-test sales, batch-run issues);
    /// and number counters, advanced with every issued number, which is audited on the
    /// issued record. Hangfire's tables and the EF history table are not EF entities.
    /// </summary>
    internal static readonly IReadOnlySet<Type> ExcludedTypes = new HashSet<Type>
    {
        typeof(AuditLog),
        typeof(Prime.Domain.Entities.SmvSimulationResult),
        typeof(Prime.Domain.Entities.SmvSimulationResultLine),
        typeof(Prime.Domain.Entities.ValuationTestSale),
        typeof(Prime.Domain.Entities.GeneralRevisionRunIssue),
        typeof(Prime.Domain.Entities.Forms.NumberSequence),
    };

    /// <summary>
    /// A workflow status value and the §48 action that setting it is. Matched by the
    /// enum member's name, so every status enum (WorkflowStatus, NoticeStatus,
    /// ExemptionStatus …) is covered without a list per entity. Other values
    /// (Submitted, PendingReview, Superseded …) stay UPDATE.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, AuditAction> StatusActions = new Dictionary<string, AuditAction>
    {
        ["Approved"] = AuditAction.Approve,
        ["Certified"] = AuditAction.Approve,
        ["Rejected"] = AuditAction.Reject,
        ["Returned"] = AuditAction.Reject,
        ["Posted"] = AuditAction.Post,
        ["Voided"] = AuditAction.Void,
        ["Void"] = AuditAction.Void,
        ["Cancelled"] = AuditAction.Cancel,
        ["Reversed"] = AuditAction.Reverse,
    };

    private static readonly ConcurrentDictionary<IEntityType, IForeignKey?> ParentKeys = new();

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        ProcessEntries(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ProcessEntries(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void ProcessEntries(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var auditLogs = new List<AuditLog>();

        foreach (EntityEntry entry in context.ChangeTracker.Entries())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted)
                || entry.Entity is not Entity
                || ExcludedTypes.Contains(entry.Metadata.ClrType))
            {
                continue;
            }

            if (entry.Entity is IAuditable auditable)
            {
                switch (entry.State)
                {
                    case EntityState.Added:
                        auditable.CreatedAt = now;
                        auditable.CreatedBy ??= currentUser.AppUserId;
                        break;
                    case EntityState.Modified:
                        auditable.UpdatedAt = now;
                        auditable.UpdatedBy = currentUser.AppUserId;
                        break;
                }
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
            _ => (StatusAction(entry), SerializeChangedOriginal(entry), SerializeChangedCurrent(entry)),
        };
        var (parentTable, parentId) = entry.Entity is IAuditable ? (null, null) : Parent(entry);

        return new AuditLog
        {
            UserId = currentUser.AppUserId,
            Module = ResolveModule(entry.Metadata.ClrType.Namespace),
            TableName = entry.Metadata.GetTableName() ?? entry.Metadata.ClrType.Name,
            RecordId = recordId,
            ParentTableName = parentTable,
            ParentRecordId = parentId,
            Action = action,
            OldValue = oldValue,
            NewValue = newValue,
            Timestamp = now,
            IpAddress = currentUser.IpAddress,
            Reason = currentUser.Reason,
        };
    }

    /// <summary>
    /// The §48 action of a modification: the named action of a status it set, else CANCEL for a record without a
    /// status that is cancelled by setting its <c>CancelledAt</c> (market-data records), else UPDATE.
    /// </summary>
    private static AuditAction StatusAction(EntityEntry entry)
    {
        foreach (var property in entry.Properties)
        {
            if (property.IsModified
                && property.Metadata.Name.EndsWith("Status", StringComparison.Ordinal)
                && property.CurrentValue is Enum value
                && !Equals(property.CurrentValue, property.OriginalValue)
                && StatusActions.TryGetValue(value.ToString(), out var action))
            {
                return action;
            }
        }
        var cancelledAt = entry.Properties.FirstOrDefault(p => p.Metadata.Name == "CancelledAt");
        return cancelledAt is { IsModified: true, OriginalValue: null, CurrentValue: not null } ? AuditAction.Cancel : AuditAction.Update;
    }

    /// <summary>
    /// The record a plain child row belongs to: the principal of the foreign key its parent navigates through
    /// (e.g. OfficeAssignment.Roles), else its first required foreign key. Resolved once per entity type.
    /// </summary>
    private static (string? Table, Guid? Id) Parent(EntityEntry entry)
    {
        var key = ParentKeys.GetOrAdd(entry.Metadata, type =>
        {
            var keys = type.GetForeignKeys().Where(fk => fk.Properties.Count == 1 && fk.PrincipalEntityType != type).ToList();
            return keys.FirstOrDefault(fk => fk.PrincipalToDependent is not null && fk.IsRequired)
                ?? keys.FirstOrDefault(fk => fk.PrincipalToDependent is not null)
                ?? keys.FirstOrDefault(fk => fk.IsRequired);
        });
        if (key is null)
        {
            return (null, null);
        }
        var property = entry.Property(key.Properties[0].Name);
        var value = entry.State == EntityState.Deleted ? property.OriginalValue : property.CurrentValue;
        return value is Guid id ? (key.PrincipalEntityType.GetTableName() ?? key.PrincipalEntityType.ClrType.Name, id) : (null, null);
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
        return dict.Count == 0 ? null : JsonSerializer.Serialize(dict, ValueJson);
    }

    /// <summary>Enums by name ("Approved", not 3), so the audit viewer reads without a code list (P12-3). Rows written
    /// before 2026-10-08 keep their numbers: the log is append-only.</summary>
    private static readonly JsonSerializerOptions ValueJson = new() { Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } };

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
