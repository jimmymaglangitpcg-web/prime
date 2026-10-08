namespace Prime.Domain.Enums;

/// <summary>
/// Whether a user may work in PRIME (docs/analysis/workflow-security.md §4.2). The numbers continue the
/// <see cref="RecordStatus"/> values the column held before, so existing users stay Active.
/// </summary>
public enum AppUserStatus
{
    /// <summary>May work, within the permissions of their office assignment.</summary>
    Active = 0,

    /// <summary>Disabled: refused on every request, whatever their token.</summary>
    Inactive = 1,

    /// <summary>Signed up, waiting for a SYSTEM_ADMIN to approve the account; sees only who they are and their sign-up request.</summary>
    Pending = 2,
}
