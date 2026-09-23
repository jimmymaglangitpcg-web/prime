using Prime.Domain.Common;

namespace Prime.Domain.Entities.Identity;

/// <summary>
/// Fine-grained permission (e.g. "assessment:approve", "payment:reverse")
/// per CLAUDE.md §9 "Use permission-based authorization in addition to
/// roles."
/// </summary>
public sealed class Permission : AuditableEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Module { get; set; } = string.Empty;

    public ICollection<RolePermission> RolePermissions { get; set; } = [];
}
