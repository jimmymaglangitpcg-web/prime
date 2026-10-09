using Prime.Domain.Common;
using Prime.Domain.Entities.Identity;
using Prime.Domain.Entities.Reference;
using Prime.Domain.Enums;

namespace Prime.Domain.Entities.Offices;

/// <summary>
/// An assessor's office using PRIME (CLAUDE.md §117;
/// docs/analysis/province-wide-operation.md §3.1): the Provincial Assessor's
/// Office or a municipal assessor's office. The kind is fixed; the offices
/// themselves are configuration. An office alone grants nothing: access comes
/// from approved <see cref="OfficeJurisdiction"/>s and <see cref="OfficeAssignment"/>s.
/// </summary>
public sealed class Office : AuditableEntity, IVersioned
{
    /// <summary>Row version (xmin): optimistic concurrency, see <see cref="IVersioned"/>.</summary>
    public uint RowVersion { get; set; }

    /// <summary>Stable code, never changed once created.</summary>
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public OfficeKind Kind { get; set; }
    /// <summary>
    /// The local government named above the office on its letterhead, as printed, e.g.
    /// "Province of …" or "Municipality of …" (§3.6). Blank prints none: an office's letterhead never
    /// borrows another LGU's name from the <c>Lgu:</c> settings.
    /// </summary>
    public string? LguName { get; set; }
    /// <summary>The head's position title as printed, e.g. "Municipal Assessor".</summary>
    public string? HeadPosition { get; set; }
    /// <summary>The Sanggunian whose tax ordinance the TD cites, e.g. "Sangguniang Panlalawigan" (LAM Annex I-G p.167; Q8).</summary>
    public string? SanggunianName { get; set; }
    public string? Address { get; set; }
    public string? Contact { get; set; }
    public RecordStatus Status { get; set; } = RecordStatus.Active;
}

/// <summary>
/// A municipality covered by a municipal office for a period (§3.1). Draft →
/// approved by a second user; approving a newer version for the same
/// municipality ends the previous one, so a municipality has one office on
/// any date. The provincial office covers the whole province and has none.
/// </summary>
public sealed class OfficeJurisdiction : EffectiveDatedConfiguration
{
    public Guid OfficeId { get; set; }
    public Office? Office { get; set; }
    public Guid MunicipalityId { get; set; }
    public Municipality? Municipality { get; set; }
}

/// <summary>
/// A user's office and the roles held there, for a period (§3.2; CLAUDE.md §9).
/// One approved assignment per user at a time: approving a newer one ends the
/// previous. <see cref="OfficeId"/> null is a province-wide assignment, allowed
/// only for <see cref="RoleCodes.ProvinceWide"/> roles.
/// </summary>
public sealed class OfficeAssignment : EffectiveDatedConfiguration
{
    public Guid AppUserId { get; set; }
    public AppUser? AppUser { get; set; }
    public Guid? OfficeId { get; set; }
    public Office? Office { get; set; }
    public List<OfficeAssignmentRole> Roles { get; set; } = [];
}

public sealed class OfficeAssignmentRole : Entity
{
    public Guid OfficeAssignmentId { get; set; }
    public Guid RoleId { get; set; }
    public Role? Role { get; set; }
}
