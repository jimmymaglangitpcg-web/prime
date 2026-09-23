namespace Prime.Domain.Entities.Identity;

/// <summary>Pure join entity — composite key (RoleId, PermissionId).</summary>
public sealed class RolePermission
{
    public Guid RoleId { get; set; }
    public Role? Role { get; set; }
    public Guid PermissionId { get; set; }
    public Permission? Permission { get; set; }

    public DateTimeOffset AssignedAt { get; set; }
    public Guid? AssignedBy { get; set; }
}
