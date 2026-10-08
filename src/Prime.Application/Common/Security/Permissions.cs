namespace Prime.Application.Common.Security;

/// <summary>One capability the code checks (docs/analysis/workflow-security.md §4.1, Q1).</summary>
public sealed record PermissionDefinition(string Code, string Name, string Module);

/// <summary>
/// The permission catalogue (CLAUDE.md §9, §47; docs/analysis/workflow-security.md §4.1). A permission is a capability the
/// code checks — every API action declares one — not an LGU rule. Which role holds which permission is configuration
/// (<c>RolePermissions</c>), changed by an administrator and approved by a second user. <see cref="DefaultGrants"/> is the
/// provisional default a new installation starts with: DOMAIN VERIFICATION REQUIRED against the province's actual
/// positions and delegations (Q2).
/// </summary>
public static class Permissions
{
    // Every signed-in, assigned user: dashboards, look-up lists, the approvals inbox.
    public const string PrimeUse = "prime.use";

    public const string PropertyView = "property.view";
    public const string PropertyEdit = "property.edit";
    public const string TaxpayerView = "taxpayer.view";
    public const string TaxpayerEdit = "taxpayer.edit";
    public const string TaxpayerViewPersonal = "taxpayer.view-personal";
    public const string AppraisalPrepare = "appraisal.prepare";
    public const string AssessmentPrepare = "assessment.prepare";
    public const string AssessmentApprove = "assessment.approve";
    public const string AssessmentPost = "assessment.post";
    public const string TdPrepare = "td.prepare";
    public const string TdApprove = "td.approve";
    public const string TransactionPrepare = "transaction.prepare";
    public const string TransactionApprove = "transaction.approve";
    public const string NoticeIssue = "notice.issue";
    public const string ExemptionPrepare = "exemption.prepare";
    public const string ExemptionApprove = "exemption.approve";
    public const string RecordsView = "records.view";
    public const string RecordsRun = "records.run";
    public const string RecordsApprove = "records.approve";
    public const string RecordsExport = "records.export";
    public const string FormsPreview = "forms.preview";
    public const string FormsIssue = "forms.issue";
    public const string ConfigEdit = "config.edit";
    public const string ConfigApprove = "config.approve";
    public const string SmvView = "smv.view";
    public const string SmvPrepare = "smv.prepare";
    public const string MarketView = "market.view";
    public const string MarketEdit = "market.edit";
    public const string GisView = "gis.view";
    public const string GisEdit = "gis.edit";
    public const string PinManage = "pin.manage";
    public const string PinApprove = "pin.approve";
    public const string GeneralRevisionView = "gr.view";
    public const string GeneralRevisionManage = "gr.manage";
    public const string OfficeView = "office.view";
    public const string UsersManage = "users.manage";
    public const string UsersApprove = "users.approve";
    /// <summary>Approve or reject sign-up requests: SYSTEM_ADMIN by default (workflow-security.md §4.2, Q5).</summary>
    public const string UsersSignUpDecide = "users.signup";
    public const string AuditView = "audit.view";
    /// <summary>The frozen billing and collection code (CLAUDE.md §0): granted to no role by default (Q18).</summary>
    public const string TreasuryLegacy = "treasury.legacy";

    /// <summary>Never granted: what an API action that declares no permission requires, so it is refused (§4.1).</summary>
    public const string Undeclared = "undeclared";

    public static readonly IReadOnlyList<PermissionDefinition> All =
    [
        new(PrimeUse, "Use PRIME: dashboard, look-up lists, approvals inbox", "General"),
        new(PropertyView, "View properties, units, appraisals, assessments, TDs and notices", "Properties"),
        new(PropertyEdit, "Register and edit properties, parcels, land, buildings, machinery and sworn statements", "Properties"),
        new(TaxpayerView, "View owners and administrators", "Taxpayers"),
        new(TaxpayerEdit, "Register and edit owners and ownership", "Taxpayers"),
        new(TaxpayerViewPersonal, "See personal data unmasked (TIN, contact details, full address)", "Taxpayers"),
        new(AppraisalPrepare, "Compute valuations and record independent appraisals", "Appraisal"),
        new(AssessmentPrepare, "Prepare and submit assessments and back taxes", "Assessment"),
        new(AssessmentApprove, "Approve or reject assessments", "Assessment"),
        new(AssessmentPost, "Post approved assessments", "Assessment"),
        new(TdPrepare, "Prepare tax declarations and their annotations", "Tax declarations"),
        new(TdApprove, "Approve, reject and cancel tax declarations", "Tax declarations"),
        new(TransactionPrepare, "Open, prepare and submit property transactions", "Transactions"),
        new(TransactionApprove, "Approve or reject property transactions", "Transactions"),
        new(NoticeIssue, "Generate, issue and serve notices", "Notices"),
        new(ExemptionPrepare, "Record exemption claims and evidence", "Exemptions"),
        new(ExemptionApprove, "Approve, reject and end exemptions", "Exemptions"),
        new(RecordsView, "View registers, submissions and issued forms", "Records"),
        new(RecordsRun, "Run registers and submit them to the province", "Records"),
        new(RecordsApprove, "Acknowledge or return submissions", "Records"),
        new(RecordsExport, "Download exports containing personal data", "Records"),
        new(FormsPreview, "Preview forms", "Forms"),
        new(FormsIssue, "Issue and cancel issued forms", "Forms"),
        new(ConfigEdit, "Prepare configuration: SMVs, levels, factors, tables, forms, numbering, chains, content packs", "Configuration"),
        new(ConfigApprove, "Approve configuration", "Configuration"),
        new(SmvView, "View SMV preparation, analyses, simulations and studies", "SMV preparation"),
        new(SmvPrepare, "Prepare SMVs: work files, analyses, simulations, tests, studies", "SMV preparation"),
        new(MarketView, "View market data", "Market data"),
        new(MarketEdit, "Record, review and import market data", "Market data"),
        new(GisView, "View maps and layers", "GIS"),
        new(GisEdit, "Edit parcel geometry and import layers", "GIS"),
        new(PinManage, "Manage index numbers, sections, PINs and territorial changes", "Identification"),
        new(PinApprove, "Approve territorial changes", "Identification"),
        new(GeneralRevisionView, "View general revision programmes", "General revision"),
        new(GeneralRevisionManage, "Run general revision programmes", "General revision"),
        new(OfficeView, "View offices, assignments, users and roles", "Users"),
        new(UsersManage, "Manage offices, assignments, delegations, users and role permissions", "Users"),
        new(UsersApprove, "Approve office, assignment, delegation, user status and role-permission changes", "Users"),
        new(UsersSignUpDecide, "Approve or reject sign-up requests, giving the office and roles", "Users"),
        new(AuditView, "View the audit trail", "Audit"),
        new(TreasuryLegacy, "Use the frozen billing and collection screens", "Treasury (frozen)"),
    ];

    private static readonly string[] Views =
        [PrimeUse, PropertyView, TaxpayerView, RecordsView, FormsPreview, SmvView, MarketView, GisView, GeneralRevisionView, OfficeView];

    /// <summary>
    /// The provisional default matrix (Q2), by role code (CLAUDE.md §9). DOMAIN VERIFICATION REQUIRED; the administrator
    /// changes it on the role-permission screen under maker-checker.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> DefaultGrants = new Dictionary<string, IReadOnlyList<string>>
    {
        ["SYSTEM_ADMIN"] = [.. Views, UsersManage, UsersApprove, UsersSignUpDecide, ConfigEdit, AuditView],
        ["ASSESSOR"] = All.Select(p => p.Code).Where(c => c is not (UsersManage or UsersSignUpDecide or TreasuryLegacy)).ToList(),
        ["APPRAISER"] = [.. Views, PropertyEdit, TaxpayerViewPersonal, AppraisalPrepare, AssessmentPrepare, TransactionPrepare, SmvPrepare,
            MarketEdit, GeneralRevisionManage],
        ["ASSESSMENT_ENCODER"] = [.. Views, PropertyEdit, TaxpayerEdit, TaxpayerViewPersonal, AssessmentPrepare, TdPrepare, TransactionPrepare,
            NoticeIssue, ExemptionPrepare, FormsIssue],
        ["ASSESSMENT_REVIEWER"] = [.. Views, TaxpayerViewPersonal, AssessmentApprove, TdApprove, TransactionApprove, ExemptionApprove],
        ["GIS_OFFICER"] = [.. Views, GisEdit, PinManage],
        ["REPORTING_OFFICER"] = [.. Views, RecordsRun, RecordsExport, FormsIssue],
        ["AUDITOR"] = [.. Views, AuditView],
        ["VIEW_ONLY"] = Views,
    };
}
