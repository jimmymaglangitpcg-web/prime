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
/// Installs PRIME's provisional form versions at startup
/// (docs/FORMS-REVISION-PLAN.md §4.1, §5 A8) so forms can be issued before
/// the LAM arrives. Templates are embedded resources
/// (Documents/Templates/CODE.vN.liquid). Per form code:
/// <list type="bullet">
/// <item>no version yet → the latest provisional version is installed, in force from 2020-01-01;</item>
/// <item>only older provisional versions → the newer one is installed from today and ends its predecessor yesterday;</item>
/// <item>any non-provisional (e.g. LAM) version exists → nothing is touched.</item>
/// </list>
/// Seeded rows are system-approved: they carry no legal content and always
/// render watermarked. Issued forms keep the version they were issued under.
/// </summary>
public sealed class ProvisionalFormSeeder(IServiceScopeFactory scopes, ILogger<ProvisionalFormSeeder> logger) : IHostedService
{
    public const string LegalBasis = "PRIME provisional layout — not an official form (docs/FORMS-REVISION-PLAN.md)";

    /// <summary>The latest provisional version of each form.</summary>
    private static readonly (string Code, int Version, string Title, FormSubjectType Subject)[] Forms =
    [
        ("TAX_BILL", 1, "Real Property Tax Bill", FormSubjectType.TaxBill),
        ("TAX_DECLARATION", 2, "Tax Declaration of Real Property", FormSubjectType.TaxDeclaration),
    ];

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
                if (existing.Any(x => x.Authority != FormAuthority.PrimeProvisional || x.Version >= form.Version))
                {
                    continue;
                }
                var open = existing.SingleOrDefault(x => x.Status == WorkflowStatus.Approved && x.EndDate is null);
                var effective = existing.Count == 0 ? new DateOnly(2020, 1, 1) : today;
                if (open is not null && open.EffectiveDate >= effective)
                {
                    continue; // the predecessor started today; install tomorrow on the next start
                }

                await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
                if (open is not null)
                {
                    open.EndDate = effective.AddDays(-1);
                    await db.SaveChangesAsync(cancellationToken); // UX_FormDefinitions_OpenApproved is not deferrable
                }
                db.FormDefinitions.Add(new FormDefinition
                {
                    Code = form.Code, Version = form.Version, Title = form.Title, SubjectType = form.Subject,
                    Authority = FormAuthority.PrimeProvisional, LegalBasis = LegalBasis,
                    SourceReference = "PRIME provisional template", TemplateBody = ReadTemplate($"{form.Code}.v{form.Version}.liquid"),
                    EffectiveDate = effective, Status = WorkflowStatus.Approved, ApprovedAt = DateTimeOffset.UtcNow,
                });
                await db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                logger.LogInformation("Seeded provisional form {FormCode} v{Version} effective {Effective}", form.Code, form.Version, effective);
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
