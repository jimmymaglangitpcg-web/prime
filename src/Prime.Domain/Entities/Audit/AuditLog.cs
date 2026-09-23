using Prime.Domain.Common;
using Prime.Domain.Enums;

namespace Prime.Domain.Entities.Audit;

/// <summary>
/// CLAUDE.md §48. Append-only — no update/delete path exists anywhere in
/// the application layer for this entity. Written automatically by
/// <see cref="Prime.Infrastructure.Persistence.Interceptors.AuditSaveChangesInterceptor"/>
/// for every tracked mutation of an <see cref="IAuditable"/> entity; never
/// written by hand in a feature handler, so no write path can silently
/// skip auditing (docs/DATABASE.md §6).
/// </summary>
public sealed class AuditLog : Entity
{
    /// <summary>AppUser.Id of the acting user — not the raw Supabase user id.</summary>
    public Guid? UserId { get; set; }

    public string Module { get; set; } = string.Empty;
    public string TableName { get; set; } = string.Empty;
    public Guid RecordId { get; set; }

    public AuditAction Action { get; set; }

    /// <summary>JSON snapshot of changed fields before the change (null for Create).</summary>
    public string? OldValue { get; set; }

    /// <summary>JSON snapshot of changed fields after the change (null for Delete).</summary>
    public string? NewValue { get; set; }

    public DateTimeOffset Timestamp { get; set; }
    public string? IpAddress { get; set; }
    public string? Reason { get; set; }
}
