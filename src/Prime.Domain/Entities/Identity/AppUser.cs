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
    public AppUserStatus Status { get; set; } = AppUserStatus.Active;

    /// <summary>Real Estate Appraiser licence (PRC), printed with the signature (LAM Bk I p.9; records-and-forms.md §4.1, Q10).</summary>
    public string? ReaLicenceNumber { get; set; }
    public DateOnly? ReaLicenceValidUntil { get; set; }

    public ICollection<UserRole> UserRoles { get; set; } = [];
}
