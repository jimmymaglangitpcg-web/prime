using Prime.Domain.Common;
using Prime.Domain.Enums;

namespace Prime.Domain.Entities.Audit;

/// <summary>
/// CLAUDE.md §48. Append-only — no update/delete path exists anywhere in
/// the application layer for this entity. Written automatically by
/// <see cref="Prime.Infrastructure.Persistence.Interceptors.AuditSaveChangesInterceptor"/>
/// for every tracked mutation of an <see cref="IAuditable"/> entity; never
/// written by hand in a feature handler, so no write path can silently
/// skip auditing (docs/DATABASE.md §6). Events that change no record
/// (sign-in, sign-out, exports and prints) are written by <c>SecurityEventLog</c>.
/// The database refuses UPDATE, DELETE and TRUNCATE on the table
/// (docs/analysis/workflow-security.md §4.3).
/// </summary>
public sealed class AuditLog : Entity
{
    /// <summary>AppUser.Id of the acting user — not the raw Supabase user id.</summary>
    public Guid? UserId { get; set; }

    public string Module { get; set; } = string.Empty;
    public string TableName { get; set; } = string.Empty;
    public Guid RecordId { get; set; }

    /// <summary>
    /// For a child row (a plain <c>Entity</c> such as an assignment's role or a transaction's requirement): the
    /// table and id of the record it belongs to, so the parent's history shows the child's changes.
    /// </summary>
    public string? ParentTableName { get; set; }

    public Guid? ParentRecordId { get; set; }

    public AuditAction Action { get; set; }

    /// <summary>JSON snapshot of changed fields before the change (null for Create).</summary>
    public string? OldValue { get; set; }

    /// <summary>JSON snapshot of changed fields after the change (null for Delete).</summary>
    public string? NewValue { get; set; }

    public DateTimeOffset Timestamp { get; set; }
    public string? IpAddress { get; set; }
    public string? Reason { get; set; }
}
