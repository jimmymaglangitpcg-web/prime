using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Prime.Domain.Entities.Forms;
using Prime.Domain.Enums;
using Prime.Infrastructure.Persistence;

namespace Prime.Infrastructure.Documents;

/// <summary>
/// Installs PRIME's provisional form versions at startup
/// (docs/FORMS-REVISION-PLAN.md §4.1, §5 A8) so forms can be issued before
/// the LAM arrives. A form code is seeded only when it has no version at
/// all, so a deployment's own (e.g. LAM) versions are never touched. The
/// seeded rows are system-approved: they carry no legal content and always
/// render watermarked. Templates are embedded resources
/// (Documents/Templates/CODE.vN.liquid).
/// </summary>
public sealed class ProvisionalFormSeeder(IServiceScopeFactory scopes, ILogger<ProvisionalFormSeeder> logger) : IHostedService
{
    public const string LegalBasis = "PRIME provisional layout — not an official form (docs/FORMS-REVISION-PLAN.md)";

    private static readonly (string Code, int Version, string Title, FormSubjectType Subject)[] Forms =
    [
        ("TAX_BILL", 1, "Real Property Tax Bill", FormSubjectType.TaxBill),
        ("TAX_DECLARATION", 1, "Tax Declaration of Real Property", FormSubjectType.TaxDeclaration),
    ];

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<PrimeDbContext>();
            foreach (var form in Forms)
            {
                if (await db.FormDefinitions.AnyAsync(x => x.Code == form.Code, cancellationToken))
                {
                    continue;
                }
                db.FormDefinitions.Add(new FormDefinition
                {
                    Code = form.Code, Version = form.Version, Title = form.Title, SubjectType = form.Subject,
                    Authority = FormAuthority.PrimeProvisional, LegalBasis = LegalBasis,
                    SourceReference = "PRIME provisional template", TemplateBody = ReadTemplate($"{form.Code}.v{form.Version}.liquid"),
                    EffectiveDate = new DateOnly(2020, 1, 1), Status = WorkflowStatus.Approved, ApprovedAt = DateTimeOffset.UtcNow,
                });
                await db.SaveChangesAsync(cancellationToken);
                logger.LogInformation("Seeded provisional form {FormCode} v{Version}", form.Code, form.Version);
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
