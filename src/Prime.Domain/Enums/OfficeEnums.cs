namespace Prime.Domain.Enums;

/// <summary>Kinds of assessor's office (docs/analysis/province-wide-operation.md Q1).</summary>
public enum OfficeKind
{
    Provincial = 0,
    Municipal = 1,
}

/// <summary>
/// PRIME's fixed role codes (CLAUDE.md §9). They are product roles, not LGU
/// positions: the office's positions map onto them by assignment.
/// </summary>
public static class RoleCodes
{
    public const string SystemAdmin = "SYSTEM_ADMIN";
    public const string Assessor = "ASSESSOR";
    public const string Appraiser = "APPRAISER";
    public const string AssessmentEncoder = "ASSESSMENT_ENCODER";
    public const string AssessmentReviewer = "ASSESSMENT_REVIEWER";
    public const string GisOfficer = "GIS_OFFICER";
    public const string ReportingOfficer = "REPORTING_OFFICER";
    public const string Auditor = "AUDITOR";
    public const string ViewOnly = "VIEW_ONLY";

    public static readonly IReadOnlyList<(string Code, string Name)> All =
    [
        (SystemAdmin, "System Administrator"),
        (Assessor, "Assessor"),
        (Appraiser, "Appraiser"),
        (AssessmentEncoder, "Assessment Encoder"),
        (AssessmentReviewer, "Assessment Reviewer"),
        (GisOfficer, "GIS Officer"),
        (ReportingOfficer, "Reporting Officer"),
        (Auditor, "Auditor"),
        (ViewOnly, "View Only"),
    ];

    /// <summary>Roles that may be held province-wide, without an office (Q2).</summary>
    public static readonly IReadOnlySet<string> ProvinceWide = new HashSet<string> { SystemAdmin, Auditor };
}
