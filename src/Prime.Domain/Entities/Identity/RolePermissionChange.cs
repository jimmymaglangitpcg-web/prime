using Prime.Domain.Common;
using Prime.Domain.Enums;

namespace Prime.Domain.Entities.Identity;

/// <summary>
/// A proposed change to one role's permissions (docs/analysis/workflow-security.md §4.1): prepared by one user, applied
/// to <see cref="RolePermission"/> only when a second user approves it (CLAUDE.md §46). The matrix itself is configuration.
/// </summary>
public sealed class RolePermissionChange : AuditableEntity, IVersioned
{
    /// <summary>Row version (xmin): optimistic concurrency, see <see cref="IVersioned"/>.</summary>
    public uint RowVersion { get; set; }

    public Guid RoleId { get; set; }
    public Role? Role { get; set; }
    /// <summary>Permission codes the role gains.</summary>
    public List<string> Grant { get; set; } = [];
    /// <summary>Permission codes the role loses.</summary>
    public List<string> Revoke { get; set; } = [];
    public string Reason { get; set; } = string.Empty;
    public WorkflowStatus Status { get; set; } = WorkflowStatus.Draft;
    public Guid? DecidedBy { get; set; }
    public DateTimeOffset? DecidedAt { get; set; }
    public string? DecisionReason { get; set; }
}
