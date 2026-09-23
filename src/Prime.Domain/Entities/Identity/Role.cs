using Prime.Domain.Common;

namespace Prime.Domain.Entities.Identity;

/// <summary>
/// One of the 11 fixed roles in CLAUDE.md §9/§47. Fixed set, but still a
/// table (not an enum) because RolePermission — the actual capability
/// grant — must be configurable per CLAUDE.md §7/§46 without a redeploy.
/// </summary>
public sealed class Role : AuditableEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    public ICollection<RolePermission> RolePermissions { get; set; } = [];
    public ICollection<UserRole> UserRoles { get; set; } = [];
}
