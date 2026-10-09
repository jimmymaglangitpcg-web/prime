using Prime.Domain.Common;
using Prime.Domain.Enums;

namespace Prime.Domain.Entities.Offices;

/// <summary>
/// The Provincial Assessor's delegation of final approval to a municipal
/// assessor for a period (CLAUDE.md §117; LAM Book I Ch. I;
/// docs/analysis/province-wide-operation.md §3.4, Q6–Q7). A dated record, not
/// a change of approval chain: while it is in force on the signing date, the
/// municipal office's ASSESSOR signs the final step instead of the province.
/// Draft → approved by a second provincial user. It is renewed by a new record
/// that points to it, revoked early with a reason, and never deleted or
/// sub-delegated.
/// </summary>
public sealed class ApprovalDelegation : AuditableEntity, IVersioned
{
    /// <summary>Row version (xmin): optimistic concurrency, see <see cref="IVersioned"/>.</summary>
    public uint RowVersion { get; set; }

    /// <summary>The municipal office the authority is delegated to.</summary>
    public Guid OfficeId { get; set; }
    public Office? Office { get; set; }

    /// <summary>The delegating official as named in the instrument, frozen.</summary>
    public string DelegatingOfficialName { get; set; } = string.Empty;
    public string DelegatingOfficialPosition { get; set; } = string.Empty;

    /// <summary>The instrument (e.g. an office order) and its date.</summary>
    public string InstrumentReference { get; set; } = string.Empty;
    public DateOnly InstrumentDate { get; set; }

    /// <summary>The records covered. Never empty.</summary>
    public List<ApprovalSubjectType> SubjectTypes { get; set; } = [];

    /// <summary>The property kinds covered; empty covers every kind.</summary>
    public List<RpuType> PropertyKinds { get; set; } = [];

    /// <summary>First and last day of the delegation (inclusive).</summary>
    public DateOnly ValidFrom { get; set; }
    public DateOnly ValidTo { get; set; }

    public WorkflowStatus Status { get; set; } = WorkflowStatus.Draft;
    public Guid? ApprovedBy { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
    /// <summary>Why a draft was rejected.</summary>
    public string? RejectionReason { get; set; }

    /// <summary>The delegation this one renews.</summary>
    public Guid? RenewsDelegationId { get; set; }

    /// <summary>Revocation: the delegation no longer applies from <see cref="RevokedFrom"/>.</summary>
    public DateOnly? RevokedFrom { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public Guid? RevokedBy { get; set; }
    public string? RevocationReason { get; set; }

    public string? Remarks { get; set; }

    /// <summary>In force on <paramref name="date"/> (approved, within its period, not yet revoked).</summary>
    public bool InForceOn(DateOnly date) =>
        Status == WorkflowStatus.Approved && ValidFrom <= date && date <= ValidTo && (RevokedFrom is null || date < RevokedFrom);

    /// <summary>The last day it actually applies: its end, or the day before its revocation.</summary>
    public DateOnly EffectiveEnd => RevokedFrom is { } r && r.AddDays(-1) < ValidTo ? r.AddDays(-1) : ValidTo;
}
