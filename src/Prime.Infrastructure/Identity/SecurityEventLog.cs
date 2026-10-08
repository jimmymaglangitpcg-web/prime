using System.Text.Json;
using Prime.Application.Common.Interfaces;
using Prime.Domain.Entities.Audit;
using Prime.Domain.Enums;
using Prime.Infrastructure.Persistence;

namespace Prime.Infrastructure.Identity;

/// <summary>
/// The one place, besides the save interceptor, that writes <see cref="AuditLog"/> rows: events that change no record
/// (sign-in, sign-out, exports and prints).
/// </summary>
public sealed class SecurityEventLog(PrimeDbContext db, ICurrentUserService currentUser) : ISecurityEventLog
{
    public async Task WriteAsync(AuditAction action, Guid appUserId, string? reason, CancellationToken cancellationToken = default)
    {
        db.AuditLogs.Add(new AuditLog
        {
            UserId = appUserId,
            Module = "Identity",
            TableName = "AppUsers",
            RecordId = appUserId,
            Action = action,
            Timestamp = DateTimeOffset.UtcNow,
            IpAddress = currentUser.IpAddress,
            Reason = reason,
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task WriteExportAsync(ExportEvent export, CancellationToken cancellationToken = default)
    {
        db.AuditLogs.Add(new AuditLog
        {
            UserId = currentUser.AppUserId,
            Module = export.Module,
            TableName = export.TableName,
            RecordId = export.RecordId,
            Action = AuditAction.Export,
            NewValue = JsonSerializer.Serialize(new { export.What, export.Format }),
            Timestamp = DateTimeOffset.UtcNow,
            IpAddress = currentUser.IpAddress,
            Reason = currentUser.Reason,
        });
        await db.SaveChangesAsync(cancellationToken);
    }
}
