namespace Prime.Domain.Enums;

/// <summary>Fixed set per CLAUDE.md §48.</summary>
public enum AuditAction
{
    Create = 0,
    Update = 1,
    Delete = 2,
    Approve = 3,
    Reject = 4,
    Post = 5,
    Void = 6,
    Cancel = 7,
    Reverse = 8,
    Login = 9,
    Logout = 10,
    Export = 11,
}
