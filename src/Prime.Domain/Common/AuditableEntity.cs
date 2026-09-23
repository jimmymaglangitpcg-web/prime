namespace Prime.Domain.Common;

/// <summary>
/// Convenience base for the common case of an entity that is both
/// identity-bearing (<see cref="Entity"/>) and audited
/// (<see cref="IAuditable"/>). Concrete entities inherit this instead of
/// repeating the four audit properties individually.
/// </summary>
public abstract class AuditableEntity : Entity, IAuditable
{
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
}
