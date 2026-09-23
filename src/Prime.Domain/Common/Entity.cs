namespace Prime.Domain.Common;

/// <summary>
/// Base type for every Domain entity. Identity is by <see cref="Id"/>, not by
/// reference or value — matches the surrogate-key convention in
/// docs/DATABASE.md (PascalCase &lt;Entity&gt;Id primary keys).
/// </summary>
public abstract class Entity
{
    public Guid Id { get; protected set; } = Guid.NewGuid();

    public override bool Equals(object? obj)
    {
        if (obj is not Entity other || other.GetType() != GetType())
        {
            return false;
        }

        return Id == other.Id;
    }

    public override int GetHashCode() => HashCode.Combine(GetType(), Id);
}
