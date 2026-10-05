using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Prime.Application.Common.Interfaces;
using Prime.Domain.Entities.Forms;
using Prime.Domain.Enums;
using Prime.Infrastructure.Persistence;

namespace Prime.Infrastructure.Documents;

/// <summary>
/// Installs PRIME's built-in form versions at startup — its provisional
/// layouts and the MRPAAO reference layouts (docs/analysis/mrpaao-forms-model.md §13)
/// (docs/FORMS-REVISION-PLAN.md §4.1, §5 A8) so forms can be issued before
/// the LAM arrives. Templates are embedded resources
/// (Documents/Templates/CODE.vN.liquid). Per form code:
/// <list type="bullet">
/// <item>no version yet → the latest provisional version is installed, in force from 2020-01-01;</item>
/// <item>only older built-in versions → the newer one is installed from today and ends its predecessor yesterday
/// (a predecessor that itself started today is cancelled and replaced from the same day);</item>
/// <item>any version not built in (e.g. the LAM's) exists → nothing is touched.</item>
/// </list>
/// Seeded rows are system-approved: they carry no legal content and always
/// render watermarked. Issued forms keep the version they were issued under.
/// </summary>
public sealed class ProvisionalFormSeeder(IServiceScopeFactory scopes, ILogger<ProvisionalFormSeeder> logger) : IHostedService
{
    public const string LegalBasis = "PRIME provisional layout — not an official form (docs/FORMS-REVISION-PLAN.md)";

    public const string MrpaaoLegalBasis =
        "Layout of the Manual on Real Property Appraisal and Assessment Operations (DOF-BLGF, LAR 1-04, 2004/2006) — superseded by the LAM; reference layout until the LAM's forms are configured";

    /// <summary>
    /// The latest built-in version of each form: PRIME's provisional layouts, and
    /// the MRPAAO reference layouts (docs/analysis/mrpaao-forms-model.md §13).
    /// </summary>
    private static readonly (string Code, int Version, string Title, FormSubjectType Subject, FormAuthority Authority, string Source)[] Forms =
    [
        ("TAX_BILL", 1, "Real Property Tax Bill", FormSubjectType.TaxBill, FormAuthority.PrimeProvisional, "PRIME provisional template"),
        // v4 (and FAAS v2): signatures show the delegation they were given under (docs/analysis/province-wide-operation.md §3.4).
        // v5 (and FAAS v3): each assessment row says whether it is exempt and on what basis; a partly exempt TD ticks both
        // boxes (docs/analysis/assessment-listing-exemptions.md Q2).
        ("TAX_DECLARATION", 5, "Tax Declaration of Real Property", FormSubjectType.TaxDeclaration, FormAuthority.Mrpaao, "MRPAAO Attachment 4 (p.236)"),
        ("NOTICE_OF_ASSESSMENT", 2, "Notice of Assessment", FormSubjectType.NoticeOfAssessment, FormAuthority.Mrpaao, "MRPAAO Attachment 10 (p.242)"),
        ("FAAS", 1, "Field Appraisal and Assessment Sheet", FormSubjectType.Assessment, FormAuthority.PrimeProvisional, "PRIME provisional template"),
        ("STATEMENT_OF_ACCOUNT", 2, "Statement of Account — Real Property Tax", FormSubjectType.StatementOfAccount, FormAuthority.PrimeProvisional, "PRIME provisional template"),
        ("OFFICIAL_RECEIPT", 1, "Official Receipt — Real Property Tax", FormSubjectType.Payment, FormAuthority.PrimeProvisional, "PRIME provisional template; eOR minimum content per DOF DO 054-2024 §7.1"),
        ("FAAS_LAND", 3, "Real Property Field Appraisal & Assessment Sheet — Land / Other Improvements", FormSubjectType.Faas, FormAuthority.Mrpaao, "MRPAAO Attachment 1 (p.230–231)"),
        ("FAAS_BUILDING", 3, "Real Property Field Appraisal & Assessment Sheet — Building & Other Improvements", FormSubjectType.Faas, FormAuthority.Mrpaao, "MRPAAO Attachment 2 (p.232–233)"),
        ("FAAS_MACHINERY", 3, "Real Property Field Appraisal & Assessment Sheet — Machinery", FormSubjectType.Faas, FormAuthority.Mrpaao, "MRPAAO Attachment 3 (p.234–235)"),
        ("TMCR", 2, "Tax Map Control Roll", FormSubjectType.Register, FormAuthority.Mrpaao, "MRPAAO Attachment 5 (p.158–159); Ch. II Figure 10 (p.70)"),
        ("PRE_TMCR", 2, "Pre-Tax Map Control Roll", FormSubjectType.Register, FormAuthority.Mrpaao, "MRPAAO Ch. II Figure 3 (p.54)"),
        ("AR_TAXABLE", 1, "Assessment Roll — Taxable Properties", FormSubjectType.Register, FormAuthority.Mrpaao, "MRPAAO Attachment 6 (p.160–161)"),
        ("AR_EXEMPT", 1, "Assessment Roll — Exempt Properties", FormSubjectType.Register, FormAuthority.Mrpaao, "MRPAAO Attachment 7 (p.162–163)"),
        ("ORC", 1, "Ownership Record Card", FormSubjectType.Register, FormAuthority.Mrpaao, "MRPAAO Attachment 8 (p.164–166)"),
        ("ROA", 1, "Record of Assessment", FormSubjectType.Register, FormAuthority.Mrpaao, "MRPAAO Attachment 9 (p.166–167)"),
        // L3-4: the LAM describes these but gives no annex form (docs/analysis/assessment-listing-exemptions.md Q10, Q11).
        ("DISCOVERY_SUMMONS", 1, "Summons (Discovery)", FormSubjectType.DiscoverySummons, FormAuthority.PrimeProvisional, "PRIME provisional template; LAM 2025 Book III pp.85–86 describes the summons"),
        ("NOTICE_OF_CANCELLATION", 1, "Notice of Cancellation", FormSubjectType.NoticeOfCancellation, FormAuthority.PrimeProvisional, "PRIME provisional template; LAM 2025 Book III p.89 C requires the notice"),
        // L6-1: market data lists (docs/analysis/smv-preparation-general-revision.md §4.1); the LAM's annex forms are loaded as content.
        ("MARKET_ABSTRACT_TRANSACTIONS", 1, "Abstract of Registered Real Property Transactions", FormSubjectType.MarketDataReport, FormAuthority.PrimeProvisional, "PRIME provisional template; LAM 2025 Book I p.22 describes the abstract"),
        ("MARKET_ABSTRACT_BUILDING_PERMITS", 1, "Abstract of Building Permits", FormSubjectType.MarketDataReport, FormAuthority.PrimeProvisional, "PRIME provisional template; LAM 2025 Book I pp.22–23 describes the abstract"),
        ("MARKET_ABSTRACT_MACHINERY", 1, "Abstract of Certificates of Registration of Installation of Machinery", FormSubjectType.MarketDataReport, FormAuthority.PrimeProvisional, "PRIME provisional template; LAM 2025 Book I p.23 describes the abstract"),
        ("MARKET_SALES_REPORT", 1, "Report of Lowest to Highest Recorded Sales of Real Properties", FormSubjectType.MarketDataReport, FormAuthority.PrimeProvisional, "PRIME provisional template; LAM 2025 Book I p.25 describes the report"),
        // L6-6c: the general revision's reports, printed from the programme (docs/analysis/smv-preparation-general-revision.md §4.6).
        ("GR_COMPLETION_REPORT", 1, "General Revision — Completion Report", FormSubjectType.GeneralRevision, FormAuthority.PrimeProvisional, "PRIME provisional template; LAM 2025 Book IV p.125 (GRI 19) requires the report"),
        // L6-2c: the SMV forms (docs/analysis/smv-preparation-general-revision.md §4.2); the LAM's layouts are loaded as content.
        ("SMV_FORM_1", 1, "Sub-Classification Criteria", FormSubjectType.Smv, FormAuthority.PrimeProvisional, "PRIME provisional template; LAM 2025 Book IV pp.114–115 lists the form (Annex IV-A)"),
        ("SMV_FORM_2", 1, "Statement of Sales Values of Residential, Commercial and Industrial Lands", FormSubjectType.SalesAnalysis, FormAuthority.PrimeProvisional, "PRIME provisional template; LAM 2025 Book IV pp.114–115 lists the form (Annex IV-B)"),
        ("SMV_FORM_3", 1, "Tabulation of Sales Values (Residential, Commercial and Industrial Lands)", FormSubjectType.SalesAnalysis, FormAuthority.PrimeProvisional, "PRIME provisional template; LAM 2025 Book IV pp.114–115 lists the form (Annex IV-C)"),
        ("SMV_FORM_4", 1, "Computation for the Unit Base Market Value", FormSubjectType.SalesAnalysis, FormAuthority.PrimeProvisional, "PRIME provisional template; LAM 2025 Book IV pp.114–115 lists the form (Annex IV-D)"),
        ("SMV_FORM_5", 1, "Schedule of Base Unit Market Values for Residential, Commercial and Industrial Lands", FormSubjectType.Smv, FormAuthority.PrimeProvisional, "PRIME provisional template; LAM 2025 Book IV pp.114–115 lists the form (Annex IV-E)"),
        ("SMV_FORM_6", 1, "Statement of Sales Values of Agricultural Lands", FormSubjectType.SalesAnalysis, FormAuthority.PrimeProvisional, "PRIME provisional template; LAM 2025 Book IV pp.114–115 lists the form (Annex IV-F)"),
        ("SMV_FORM_7", 1, "Tabulation of Sales Values of Agricultural Lands", FormSubjectType.SalesAnalysis, FormAuthority.PrimeProvisional, "PRIME provisional template; LAM 2025 Book IV pp.114–115 lists the form (Annex IV-G)"),
        ("SMV_FORM_8", 1, "Computation for the Unit Base Market Value of Agricultural Lands", FormSubjectType.SalesAnalysis, FormAuthority.PrimeProvisional, "PRIME provisional template; LAM 2025 Book IV pp.114–115 lists the form (Annex IV-H)"),
        ("SMV_FORM_9", 1, "Schedule of Base Unit Market Values for Agricultural Lands", FormSubjectType.Smv, FormAuthority.PrimeProvisional, "PRIME provisional template; LAM 2025 Book IV pp.114–115 lists the form (Annex IV-I)"),
        ("SMV_FORM_10", 1, "Schedule of Base Unit Construction Cost for Buildings", FormSubjectType.Smv, FormAuthority.PrimeProvisional, "PRIME provisional template; LAM 2025 Book IV pp.114–115 lists the form (Annex IV-J)"),
        ("SMV_FORM_11", 1, "Schedule of Physical Depreciation", FormSubjectType.Smv, FormAuthority.PrimeProvisional, "PRIME provisional template; LAM 2025 Book IV pp.114–115 lists the form (Annex IV-K)"),
        ("SMV_FORM_12", 1, "Schedule of Unit Cost for Extra Items as Component Parts of Building", FormSubjectType.Smv, FormAuthority.PrimeProvisional, "PRIME provisional template; LAM 2025 Book IV pp.114–115 lists the form (Annex IV-L)"),
        // L6-5: the LAM describes the study and its report but gives no form (smv-preparation-general-revision.md §4.5).
        ("REVENUE_TAX_IMPACT_REPORT", 1, "Revenue and Tax Impact Report", FormSubjectType.RevenueImpactStudy, FormAuthority.PrimeProvisional, "PRIME provisional template; RA 12001 §17 and LAM 2025 Book IV pp.116–118 describe the study"),
        // L6-3: the LAM describes valuation testing but gives no form (docs/analysis/smv-preparation-general-revision.md §4.3).
        ("VALUATION_TEST_REPORT", 1, "Valuation Testing Report", FormSubjectType.ValuationTest, FormAuthority.PrimeProvisional, "PRIME provisional template; LAM 2025 Book IV pp.115–116 describes valuation testing"),
        ("GR_STATUS_REPORT", 1, "General Revision Status Report", FormSubjectType.GeneralRevision, FormAuthority.PrimeProvisional, "PRIME provisional template; LAM 2025 Book I p.25 describes the report"),
        ("SWORN_STATEMENT", 1, "Sworn Statement of the True Current and Fair Market Value of Real Properties", FormSubjectType.SwornStatement, FormAuthority.Mrpaao, "MRPAAO Attachment 11 (p.243–244)"),
    ];

    /// <summary>Authorities PRIME installs itself; any other version (e.g. the LAM's) is never touched.</summary>
    private static bool BuiltIn(FormAuthority authority) => authority is FormAuthority.PrimeProvisional or FormAuthority.Mrpaao;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<PrimeDbContext>();
            var today = scope.ServiceProvider.GetRequiredService<IClock>().Today;
            foreach (var form in Forms)
            {
                var existing = await db.FormDefinitions.Where(x => x.Code == form.Code).ToListAsync(cancellationToken);
                if (existing.Any(x => !BuiltIn(x.Authority) || x.Version >= form.Version))
                {
                    continue;
                }
                var open = existing.SingleOrDefault(x => x.Status == WorkflowStatus.Approved && x.EndDate is null);
                var effective = existing.Count == 0 ? new DateOnly(2020, 1, 1) : today;

                await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
                if (open is not null && open.EffectiveDate >= effective)
                {
                    // A built-in version that started today is replaced from the same day: it is cancelled, not
                    // deleted, and forms already issued under it keep pointing to it.
                    effective = open.EffectiveDate;
                    open.Remarks = $"Superseded on its first day by built-in v{form.Version} (was approved {open.ApprovedAt:yyyy-MM-dd HH:mm} UTC).";
                    open.Status = WorkflowStatus.Cancelled;
                    open.ApprovedAt = null;
                    await db.SaveChangesAsync(cancellationToken); // UX_FormDefinitions_OpenApproved is not deferrable
                }
                else if (open is not null)
                {
                    open.EndDate = effective.AddDays(-1);
                    await db.SaveChangesAsync(cancellationToken);
                }
                db.FormDefinitions.Add(new FormDefinition
                {
                    Code = form.Code, Version = form.Version, Title = form.Title, SubjectType = form.Subject,
                    Authority = form.Authority, LegalBasis = form.Authority == FormAuthority.Mrpaao ? MrpaaoLegalBasis : LegalBasis,
                    SourceReference = form.Source, TemplateBody = ReadTemplate($"{form.Code}.v{form.Version}.liquid"),
                    EffectiveDate = effective, Status = WorkflowStatus.Approved, ApprovedAt = DateTimeOffset.UtcNow,
                });
                await db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                logger.LogInformation("Seeded built-in form {FormCode} v{Version} ({Authority}) effective {Effective}", form.Code, form.Version, form.Authority, effective);
            }
        }
        catch (Exception ex) when (ex is DbUpdateException or Npgsql.NpgsqlException or InvalidOperationException)
        {
            // Never block startup (e.g. migrations not yet applied); forms simply stay unconfigured.
            logger.LogWarning(ex, "Provisional form seeding skipped");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public static string ReadTemplate(string fileName)
    {
        var assembly = typeof(ProvisionalFormSeeder).Assembly;
        var name = assembly.GetManifestResourceNames().Single(n => n.EndsWith($".Templates.{fileName}", StringComparison.Ordinal));
        using var stream = assembly.GetManifestResourceStream(name)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
