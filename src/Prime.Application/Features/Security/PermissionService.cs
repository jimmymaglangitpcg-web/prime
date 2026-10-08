using Microsoft.EntityFrameworkCore;
using Prime.Application.Common.Interfaces;
using Prime.Application.Features.Offices;

namespace Prime.Application.Features.Security;

/// <summary>The acting user's permissions (docs/analysis/workflow-security.md §4.1).</summary>
public interface IPermissionService
{
    /// <summary>
    /// The permissions of the roles the user holds through their office assignment in force today (LP); none without
    /// one, or while the account is not active. Resolved once per user per request.
    /// </summary>
    Task<IReadOnlySet<string>> GetAsync(CancellationToken cancellationToken = default);

    Task<bool> HasAsync(string permission, CancellationToken cancellationToken = default);
}

public sealed class PermissionService(IApplicationDbContext db, IOfficeContext offices, ICurrentUserService currentUser) : IPermissionService
{
    private (Guid? UserId, IReadOnlySet<string> Permissions)? cached;

    public async Task<IReadOnlySet<string>> GetAsync(CancellationToken cancellationToken = default)
    {
        var userId = currentUser.AppUserId;
        if (cached is { } hit && hit.UserId == userId)
        {
            return hit.Permissions;
        }
        var scope = await offices.GetAsync(cancellationToken);
        // Only an active account holds permissions: a pending sign-up has none, whatever it is assigned (§4.2).
        var active = userId is not null
            && await db.AppUsers.AsNoTracking().AnyAsync(u => u.Id == userId && u.Status == Domain.Enums.AppUserStatus.Active, cancellationToken);
        var roles = active ? scope.Roles : [];
        IReadOnlySet<string> permissions = roles.Count == 0
            ? new HashSet<string>(StringComparer.Ordinal)
            : (await db.RolePermissions.AsNoTracking().Where(rp => roles.Contains(rp.Role!.Code)).Select(rp => rp.Permission!.Code)
                .Distinct().ToListAsync(cancellationToken)).ToHashSet(StringComparer.Ordinal);
        cached = (userId, permissions);
        return permissions;
    }

    public async Task<bool> HasAsync(string permission, CancellationToken cancellationToken = default) =>
        (await GetAsync(cancellationToken)).Contains(permission);
}
