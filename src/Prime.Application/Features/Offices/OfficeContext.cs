using Microsoft.EntityFrameworkCore;
using Prime.Application.Common;
using Prime.Application.Common.Interfaces;
using Prime.Domain.Enums;

namespace Prime.Application.Features.Offices;

/// <summary>
/// The acting user's office for the current request (docs/analysis/province-wide-operation.md §3.2):
/// their approved assignment in force today, the roles held there and the
/// municipalities the office covers today.
/// </summary>
/// <param name="MunicipalityIds">
/// The municipalities the user may work in: null for the whole province (the
/// provincial office, or a province-wide assignment); empty when the user has
/// no assignment in force.
/// </param>
public sealed record OfficeScope(
    Guid? UserId,
    Guid? OfficeId,
    string? OfficeCode,
    string? OfficeName,
    OfficeKind? OfficeKind,
    IReadOnlyList<string> Roles,
    IReadOnlyList<Guid>? MunicipalityIds)
{
    public bool Assigned => MunicipalityIds is null || OfficeId is not null;

    /// <summary>Sees and works in the whole province.</summary>
    public bool ProvinceWide => MunicipalityIds is null;

    public bool HasRole(string code) => Roles.Contains(code);

    public static OfficeScope Unassigned(Guid? userId) => new(userId, null, null, null, null, [], []);
}

public interface IOfficeContext
{
    /// <summary>The current user's office scope, resolved once per user per request.</summary>
    Task<OfficeScope> GetAsync(CancellationToken cancellationToken = default);
}

public sealed class OfficeContext(IApplicationDbContext db, ICurrentUserService currentUser, IClock clock) : IOfficeContext
{
    private (Guid? UserId, OfficeScope Scope)? cached;

    public async Task<OfficeScope> GetAsync(CancellationToken cancellationToken = default)
    {
        // Keyed by user: background jobs and tests switch the acting user within one scope.
        var userId = currentUser.AppUserId;
        if (cached is { } hit && hit.UserId == userId)
        {
            return hit.Scope;
        }
        var scope = await LoadAsync(db, userId, clock.Today, cancellationToken);
        cached = (userId, scope);
        return scope;
    }

    public static async Task<OfficeScope> LoadAsync(IApplicationDbContext db, Guid? userId, DateOnly asOf, CancellationToken ct)
    {
        if (userId is null)
        {
            return OfficeScope.Unassigned(null);
        }
        var assignment = await db.OfficeAssignments.AsNoTracking()
            .Include(x => x.Office).Include(x => x.Roles).ThenInclude(r => r.Role)
            .InForce(asOf).FirstOrDefaultAsync(x => x.AppUserId == userId, ct);
        if (assignment is null || assignment.Office is { Status: not RecordStatus.Active })
        {
            return OfficeScope.Unassigned(userId);
        }

        var roles = assignment.Roles.Select(r => r.Role!.Code).Order(StringComparer.Ordinal).ToList();
        var office = assignment.Office;
        if (office is null || office.Kind == OfficeKind.Provincial)
        {
            return new OfficeScope(userId, office?.Id, office?.Code, office?.Name, office?.Kind, roles, null);
        }
        var municipalities = await db.OfficeJurisdictions.AsNoTracking().InForce(asOf)
            .Where(j => j.OfficeId == office.Id).Select(j => j.MunicipalityId).ToListAsync(ct);
        return new OfficeScope(userId, office.Id, office.Code, office.Name, office.Kind, roles, municipalities);
    }
}
