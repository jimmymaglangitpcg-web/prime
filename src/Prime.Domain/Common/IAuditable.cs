namespace Prime.Domain.Common;

/// <summary>
/// Marks an entity as subject to the audit interceptor
/// (Prime.Infrastructure.Persistence.Interceptors.AuditSaveChangesInterceptor).
/// Every entity implementing this has its create/update captured in AuditLog
/// automatically — no feature handler writes AuditLog rows by hand.
/// See docs/DATABASE.md §6 and CLAUDE.md §48/§77.
/// </summary>
public interface IAuditable
{
    DateTimeOffset CreatedAt { get; set; }
    Guid? CreatedBy { get; set; }
    DateTimeOffset? UpdatedAt { get; set; }
    Guid? UpdatedBy { get; set; }
}
