using Microsoft.EntityFrameworkCore;
using Prime.Application.Common;
using Prime.Application.Common.Interfaces;
using Prime.Application.Common.Security;
using Prime.Domain.Entities.Identity;
using Prime.Domain.Enums;

namespace Prime.Application.Features.Security;

public sealed record PermissionDto(string Code, string Name, string Module);
public sealed record RoleGrantsDto(string RoleCode, string RoleName, IReadOnlyList<string> Permissions);
public sealed record RolePermissionChangeDto(Guid Id, string RoleCode, string RoleName, IReadOnlyList<string> Grant, IReadOnlyList<string> Revoke,
    string Reason, WorkflowStatus Status, Guid? CreatedBy, DateTimeOffset CreatedAt, Guid? DecidedBy, DateTimeOffset? DecidedAt, string? DecisionReason);
public sealed record PermissionMatrixDto(IReadOnlyList<PermissionDto> Permissions, IReadOnlyList<RoleGrantsDto> Roles,
    IReadOnlyList<RolePermissionChangeDto> Changes);

/// <param name="Permissions">The role's permissions as they should be after the change.</param>
public sealed record ProposeRolePermissionsRequest(string RoleCode, IReadOnlyList<string> Permissions, string Reason);
public sealed record DecideRolePermissionChangeRequest(string? Reason);

public interface IRolePermissionService
{
    Task<Result<PermissionMatrixDto>> GetMatrixAsync(CancellationToken ct = default);
    Task<Result<RolePermissionChangeDto>> ProposeAsync(ProposeRolePermissionsRequest request, CancellationToken ct = default);
    Task<Result<RolePermissionChangeDto>> ApproveAsync(Guid id, CancellationToken ct = default);
    Task<Result<RolePermissionChangeDto>> RejectAsync(Guid id, DecideRolePermissionChangeRequest request, CancellationToken ct = default);
}

/// <summary>
/// The role–permission matrix (docs/analysis/workflow-security.md §4.1, Q2): configuration, changed by proposing a role's
/// new set of permissions; the change applies only when a second user approves it (CLAUDE.md §46).
/// </summary>
public sealed class RolePermissionService(IApplicationDbContext db, ICurrentUserService currentUser, IClock clock) : IRolePermissionService
{
    public async Task<Result<PermissionMatrixDto>> GetMatrixAsync(CancellationToken ct = default)
    {
        var permissions = await db.Permissions.AsNoTracking().OrderBy(p => p.Module).ThenBy(p => p.Code)
            .Select(p => new PermissionDto(p.Code, p.Name, p.Module)).ToListAsync(ct);
        var roles = await db.Roles.AsNoTracking().OrderBy(r => r.Code)
            .Select(r => new RoleGrantsDto(r.Code, r.Name, r.RolePermissions.Select(rp => rp.Permission!.Code).OrderBy(c => c).ToList()))
            .ToListAsync(ct);
        var changes = await db.RolePermissionChanges.AsNoTracking().Include(c => c.Role).OrderByDescending(c => c.CreatedAt).Take(50).ToListAsync(ct);
        return Result.Success(new PermissionMatrixDto(permissions, roles, changes.Select(Dto).ToList()));
    }

    public async Task<Result<RolePermissionChangeDto>> ProposeAsync(ProposeRolePermissionsRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length > 500)
        {
            return Result.Failure<RolePermissionChangeDto>("VALIDATION_FAILED", "A reason (max 500) is required.");
        }
        var role = await db.Roles.Include(r => r.RolePermissions).ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(r => r.Code == request.RoleCode, ct);
        if (role is null)
        {
            return Result.Failure<RolePermissionChangeDto>("ROLE_NOT_FOUND", "The role does not exist.");
        }
        var wanted = (request.Permissions ?? []).Select(p => p.Trim()).ToHashSet(StringComparer.Ordinal);
        var known = await db.Permissions.Select(p => p.Code).ToListAsync(ct);
        if (wanted.FirstOrDefault(p => !known.Contains(p)) is { } unknown)
        {
            return Result.Failure<RolePermissionChangeDto>("PERMISSION_NOT_FOUND", $"Permission '{unknown}' does not exist.");
        }
        if (await db.RolePermissionChanges.AnyAsync(c => c.RoleId == role.Id && c.Status == WorkflowStatus.Draft, ct))
        {
            return Result.Failure<RolePermissionChangeDto>("ROLE_PERMISSION_CHANGE_PENDING", "This role already has a change waiting for approval.");
        }
        var current = role.RolePermissions.Select(rp => rp.Permission!.Code).ToHashSet(StringComparer.Ordinal);
        var change = new RolePermissionChange
        {
            RoleId = role.Id,
            Grant = wanted.Except(current).Order(StringComparer.Ordinal).ToList(),
            Revoke = current.Except(wanted).Order(StringComparer.Ordinal).ToList(),
            Reason = request.Reason.Trim(),
        };
        if (change.Grant.Count == 0 && change.Revoke.Count == 0)
        {
            return Result.Failure<RolePermissionChangeDto>("ROLE_PERMISSION_UNCHANGED", "The role already has exactly these permissions.");
        }
        db.RolePermissionChanges.Add(change);
        await db.SaveChangesAsync(ct);
        change.Role = role;
        return Result.Success(Dto(change));
    }

    public async Task<Result<RolePermissionChangeDto>> ApproveAsync(Guid id, CancellationToken ct = default)
    {
        var change = await db.RolePermissionChanges.Include(c => c.Role).ThenInclude(r => r!.RolePermissions).ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(c => c.Id == id, ct);
        if (await DecisionProblemAsync(change) is { } problem)
        {
            return problem;
        }
        var role = change!.Role!;
        var permissions = await db.Permissions.Where(p => change.Grant.Contains(p.Code)).ToListAsync(ct);
        var now = clock.UtcNow;
        var revoked = role.RolePermissions.Where(rp => change.Revoke.Contains(rp.Permission!.Code)).ToList();
        foreach (var permission in permissions.Where(p => role.RolePermissions.All(rp => rp.PermissionId != p.Id)))
        {
            db.RolePermissions.Add(new RolePermission
            {
                RoleId = role.Id, PermissionId = permission.Id, Permission = permission, AssignedAt = now, AssignedBy = currentUser.AppUserId,
            });
        }
        db.RolePermissions.RemoveRange(revoked);
        Decide(change, WorkflowStatus.Approved, null);
        await db.SaveChangesAsync(ct);
        return Result.Success(Dto(change));
    }

    public async Task<Result<RolePermissionChangeDto>> RejectAsync(Guid id, DecideRolePermissionChangeRequest request, CancellationToken ct = default)
    {
        var change = await db.RolePermissionChanges.Include(c => c.Role).FirstOrDefaultAsync(c => c.Id == id, ct);
        if (await DecisionProblemAsync(change) is { } problem)
        {
            return problem;
        }
        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length > 500)
        {
            return Result.Failure<RolePermissionChangeDto>("VALIDATION_FAILED", "A reason (max 500) is required to reject.");
        }
        Decide(change!, WorkflowStatus.Rejected, request.Reason.Trim());
        await db.SaveChangesAsync(ct);
        return Result.Success(Dto(change!));
    }

    private Task<Result<RolePermissionChangeDto>?> DecisionProblemAsync(RolePermissionChange? change)
    {
        Result<RolePermissionChangeDto>? problem = change switch
        {
            null => Result.Failure<RolePermissionChangeDto>("ROLE_PERMISSION_CHANGE_NOT_FOUND", "No such role-permission change."),
            { Status: not WorkflowStatus.Draft } => Result.Failure<RolePermissionChangeDto>("ROLE_PERMISSION_CHANGE_DECIDED", "This change was already decided."),
            // Maker-checker (CLAUDE.md §46): never decided by its author, nor by an unknown user (docs/analysis/workflow-security.md G7).
            _ when currentUser.AppUserId is null || change.CreatedBy == currentUser.AppUserId =>
                Result.Failure<RolePermissionChangeDto>("CANNOT_APPROVE_OWN_ROLE_PERMISSION_CHANGE", "The change's author cannot also decide it."),
            _ => null,
        };
        return Task.FromResult(problem);
    }

    private void Decide(RolePermissionChange change, WorkflowStatus status, string? reason)
    {
        change.Status = status;
        change.DecidedBy = currentUser.AppUserId;
        change.DecidedAt = clock.UtcNow;
        change.DecisionReason = reason;
        currentUser.Reason = reason;
    }

    private static RolePermissionChangeDto Dto(RolePermissionChange c) => new(c.Id, c.Role?.Code ?? "", c.Role?.Name ?? "", c.Grant, c.Revoke, c.Reason,
        c.Status, c.CreatedBy, c.CreatedAt, c.DecidedBy, c.DecidedAt, c.DecisionReason);
}
