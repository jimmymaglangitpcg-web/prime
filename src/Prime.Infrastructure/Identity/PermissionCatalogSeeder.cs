using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Prime.Application.Common.Security;
using Prime.Domain.Entities.Identity;
using Prime.Infrastructure.Persistence;

namespace Prime.Infrastructure.Identity;

/// <summary>
/// Keeps the <c>Permissions</c> table in step with the catalogue in code (docs/analysis/workflow-security.md §4.1). A
/// permission seen for the first time gets the provisional default grants (<see cref="Permissions.DefaultGrants"/>);
/// grants of permissions that already exist are never touched, so the administrator's approved changes stand. Names
/// and modules are refreshed. Nothing is ever revoked here.
/// </summary>
public sealed class PermissionCatalogSeeder(IServiceScopeFactory scopes, ILogger<PermissionCatalogSeeder> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<PrimeDbContext>();
            var existing = await db.Permissions.ToDictionaryAsync(p => p.Code, StringComparer.Ordinal, cancellationToken);
            var roles = await db.Roles.ToDictionaryAsync(r => r.Code, StringComparer.Ordinal, cancellationToken);
            var now = DateTimeOffset.UtcNow;
            var added = new List<string>();
            foreach (var definition in Permissions.All)
            {
                if (existing.TryGetValue(definition.Code, out var permission))
                {
                    permission.Name = definition.Name;
                    permission.Module = definition.Module;
                    continue;
                }
                permission = new Permission { Code = definition.Code, Name = definition.Name, Module = definition.Module };
                db.Permissions.Add(permission);
                added.Add(definition.Code);
                foreach (var (roleCode, grants) in Permissions.DefaultGrants)
                {
                    if (grants.Contains(definition.Code) && roles.TryGetValue(roleCode, out var role))
                    {
                        db.RolePermissions.Add(new RolePermission { Role = role, Permission = permission, AssignedAt = now });
                    }
                }
            }
            await db.SaveChangesAsync(cancellationToken);
            if (added.Count > 0)
            {
                logger.LogInformation("Seeded {Count} permissions with their provisional default grants: {Codes}", added.Count, string.Join(", ", added));
            }
        }
        catch (DbUpdateException ex)
        {
            // Several hosts starting together (the test suite) may race on the unique codes; the winner seeded them.
            logger.LogWarning(ex, "PermissionCatalogSeeder lost a race or could not write; another instance may have seeded the catalogue.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
