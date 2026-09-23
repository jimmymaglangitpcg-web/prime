namespace Prime.Domain.Entities.Identity;

/// <summary>
/// Pure join entity — composite key (AppUserId, RoleId), not a surrogate
/// Entity/AuditableEntity, since there is no independent identity beyond
/// the pair.
/// </summary>
public sealed class UserRole
{
    public Guid AppUserId { get; set; }
    public AppUser? AppUser { get; set; }
    public Guid RoleId { get; set; }
    public Role? Role { get; set; }

    public DateTimeOffset AssignedAt { get; set; }
    public Guid? AssignedBy { get; set; }
}
