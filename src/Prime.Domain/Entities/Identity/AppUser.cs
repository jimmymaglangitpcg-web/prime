using Prime.Domain.Common;
using Prime.Domain.Enums;

namespace Prime.Domain.Entities.Identity;

/// <summary>
/// PRIME-specific user profile and authorization record. Credentials
/// themselves live in Supabase Auth (auth.users), not here —
/// <see cref="SupabaseUserId"/> is a plain value column (not a DB foreign
/// key: auth.users only exists inside a Supabase project, not the local
/// dev Postgres — see docs/DATABASE.md §1). See CLAUDE.md §47,
/// docs/ARCHITECTURE.md §3.4.
/// </summary>
public sealed class AppUser : AuditableEntity
{
    public Guid SupabaseUserId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public RecordStatus Status { get; set; } = RecordStatus.Active;

    public ICollection<UserRole> UserRoles { get; set; } = [];
}
