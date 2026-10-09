using Prime.Domain.Common;
using Prime.Domain.Enums;

namespace Prime.Domain.Entities.Identity;

/// <summary>
/// A signed-up user's request for an account (docs/analysis/workflow-security.md §4.2, Q5). The applicant states who
/// they are and the office and roles they ask for; a SYSTEM_ADMIN approves it, choosing the office and roles (which may
/// differ), or rejects it with a reason. Approval activates the user and creates their office assignment in force at
/// once: the applicant is its maker, the administrator its checker. Requests are kept; a rejected user may ask again.
/// </summary>
public sealed class SignUpRequest : AuditableEntity, IVersioned
{
    /// <summary>Row version (xmin): optimistic concurrency, see <see cref="IVersioned"/>.</summary>
    public uint RowVersion { get; set; }

    public Guid AppUserId { get; set; }
    public AppUser? AppUser { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Position { get; set; } = string.Empty;
    /// <summary>The office the applicant says they work in; null for a province-wide post.</summary>
    public Guid? RequestedOfficeId { get; set; }
    public Offices.Office? RequestedOffice { get; set; }
    /// <summary>Role codes asked for.</summary>
    public List<string> RequestedRoles { get; set; } = [];
    public string? Note { get; set; }
    /// <summary>PendingReview, then Approved or Rejected.</summary>
    public WorkflowStatus Status { get; set; } = WorkflowStatus.PendingReview;
    public Guid? DecidedBy { get; set; }
    public DateTimeOffset? DecidedAt { get; set; }
    public string? DecisionReason { get; set; }
    /// <summary>The office assignment the approval created.</summary>
    public Guid? OfficeAssignmentId { get; set; }
}

/// <summary>
/// A proposal to disable or re-enable a user (Q6): prepared by one administrator, in force the moment a second user
/// approves it. History is kept; the user's office assignment is untouched.
/// </summary>
public sealed class UserStatusChange : AuditableEntity
{
    public Guid AppUserId { get; set; }
    public AppUser? AppUser { get; set; }
    /// <summary>Inactive (disable) or Active (enable).</summary>
    public AppUserStatus NewStatus { get; set; }
    public string Reason { get; set; } = string.Empty;
    /// <summary>Draft, then Approved or Rejected.</summary>
    public WorkflowStatus Status { get; set; } = WorkflowStatus.Draft;
    public Guid? DecidedBy { get; set; }
    public DateTimeOffset? DecidedAt { get; set; }
    public string? DecisionReason { get; set; }
}
