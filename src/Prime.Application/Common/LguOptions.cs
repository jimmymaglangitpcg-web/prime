namespace Prime.Application.Common;

/// <summary>
/// "Lgu" configuration section — the deployment's branding printed on forms
/// (CLAUDE.md §58, §85). Empty by default: PRIME never prints an LGU name
/// or seal that the LGU has not supplied.
/// </summary>
public sealed class LguOptions
{
    public const string SectionName = "Lgu";

    public string? Name { get; set; }
    public string? Office { get; set; }
    public string? Province { get; set; }
    public string? Address { get; set; }

    /// <summary>IANA time zone of the LGU, e.g. "Asia/Manila". Defines "today" for business dates (<c>IClock</c>).</summary>
    public string? TimeZone { get; set; }
}
