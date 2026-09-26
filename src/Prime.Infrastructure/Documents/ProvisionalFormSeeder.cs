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
        ("TAX_DECLARATION", 3, "Tax Declaration of Real Property", FormSubjectType.TaxDeclaration, FormAuthority.Mrpaao, "MRPAAO Attachment 4 (p.236)"),
        ("NOTICE_OF_ASSESSMENT", 2, "Notice of Assessment", FormSubjectType.NoticeOfAssessment, FormAuthority.Mrpaao, "MRPAAO Attachment 10 (p.242)"),
        ("FAAS", 1, "Field Appraisal and Assessment Sheet", FormSubjectType.Assessment, FormAuthority.PrimeProvisional, "PRIME provisional template"),
        ("STATEMENT_OF_ACCOUNT", 2, "Statement of Account — Real Property Tax", FormSubjectType.StatementOfAccount, FormAuthority.PrimeProvisional, "PRIME provisional template"),
        ("OFFICIAL_RECEIPT", 1, "Official Receipt — Real Property Tax", FormSubjectType.Payment, FormAuthority.PrimeProvisional, "PRIME provisional template; eOR minimum content per DOF DO 054-2024 §7.1"),
        ("FAAS_LAND", 1, "Real Property Field Appraisal & Assessment Sheet — Land / Other Improvements", FormSubjectType.Faas, FormAuthority.Mrpaao, "MRPAAO Attachment 1 (p.230–231)"),
        ("FAAS_BUILDING", 1, "Real Property Field Appraisal & Assessment Sheet — Building & Other Improvements", FormSubjectType.Faas, FormAuthority.Mrpaao, "MRPAAO Attachment 2 (p.232–233)"),
        ("FAAS_MACHINERY", 1, "Real Property Field Appraisal & Assessment Sheet — Machinery", FormSubjectType.Faas, FormAuthority.Mrpaao, "MRPAAO Attachment 3 (p.234–235)"),
        ("TMCR", 1, "Tax Map Control Roll", FormSubjectType.Register, FormAuthority.Mrpaao, "MRPAAO Attachment 5 (p.158–159)"),
        ("AR_TAXABLE", 1, "Assessment Roll — Taxable Properties", FormSubjectType.Register, FormAuthority.Mrpaao, "MRPAAO Attachment 6 (p.160–161)"),
        ("AR_EXEMPT", 1, "Assessment Roll — Exempt Properties", FormSubjectType.Register, FormAuthority.Mrpaao, "MRPAAO Attachment 7 (p.162–163)"),
        ("ORC", 1, "Ownership Record Card", FormSubjectType.Register, FormAuthority.Mrpaao, "MRPAAO Attachment 8 (p.164–166)"),
        ("ROA", 1, "Record of Assessment", FormSubjectType.Register, FormAuthority.Mrpaao, "MRPAAO Attachment 9 (p.166–167)"),
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
