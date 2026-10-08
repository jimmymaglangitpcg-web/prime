using Microsoft.EntityFrameworkCore;
using Prime.Application.Common.Interfaces;
using Prime.Domain.Entities.Identity;
using Prime.Domain.Entities.Offices;
using Prime.Domain.Enums;
using Prime.Infrastructure.Persistence;

namespace Prime.WebApi.Authentication;

/// <summary>
/// Development only (registered with the DevAuth bypass): gives the DEMO users
/// of <see cref="DevelopmentAuthOptions.Users"/> offices to work in
/// (docs/analysis/province-wide-operation.md §3.8, Q11, Q13). It creates a DEMO
/// provincial office if the database has none, one DEMO municipal office for
/// each DEMO municipality that no office covers, and an approved assignment
/// for each configured user who has none in force. Rows are system-approved
/// and labelled DEMO. A production database never runs this: real offices
/// come from the content pack or the admin screens.
/// </summary>
public sealed class DevOfficeSeeder(IServiceScopeFactory scopes, IConfiguration configuration, ILogger<DevOfficeSeeder> logger) : IHostedService
{
    public const string LegalBasis = "DEMO development set-up — not an LGU office or appointment (docs/analysis/province-wide-operation.md §3.8)";
    private static readonly DateOnly From = new(2020, 1, 1);

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var configured = configuration.GetSection("DevAuth:Users").Get<List<DevelopmentUser>>() ?? [];
        IReadOnlyList<DevelopmentUser> users = configured.Count > 0 ? configured : DevelopmentAuthOptions.DefaultUsers;
        try
        {
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<PrimeDbContext>();
            var today = scope.ServiceProvider.GetRequiredService<IClock>().Today;
            var now = DateTimeOffset.UtcNow;

            var provincial = await db.Offices.FirstOrDefaultAsync(o => o.Kind == OfficeKind.Provincial, cancellationToken);
            if (provincial is null)
            {
                provincial = new Office { Code = "DEMO-PROV", Name = "DEMO Provincial Assessor's Office", Kind = OfficeKind.Provincial, HeadPosition = "DEMO Provincial Assessor" };
                db.Offices.Add(provincial);
            }

            // DEMO municipalities no office covers (no approved version in force, no draft waiting).
            var covered = await db.OfficeJurisdictions.Where(j => j.Status == WorkflowStatus.Draft
                    || (j.Status == WorkflowStatus.Approved && j.EffectiveDate <= today && (j.EndDate == null || j.EndDate >= today)))
                .Select(j => j.MunicipalityId).ToListAsync(cancellationToken);
            var uncovered = await db.Municipalities.Where(m => m.Name.StartsWith("DEMO") && !covered.Contains(m.Id)).OrderBy(m => m.PsgcCode)
                .ToListAsync(cancellationToken);
            foreach (var municipality in uncovered)
            {
                var code = $"DEMO-MUN-{municipality.PsgcCode}";
                var office = await db.Offices.FirstOrDefaultAsync(o => o.Code == code, cancellationToken);
                if (office is null)
                {
                    office = new Office { Code = code, Name = $"DEMO Municipal Assessor's Office — {municipality.Name}", Kind = OfficeKind.Municipal, HeadPosition = "DEMO Municipal Assessor" };
                    db.Offices.Add(office);
                }
                db.OfficeJurisdictions.Add(new OfficeJurisdiction
                {
                    Office = office, MunicipalityId = municipality.Id, EffectiveDate = From, LegalBasis = LegalBasis,
                    Status = WorkflowStatus.Approved, ApprovedAt = now,
                });
            }
            await db.SaveChangesAsync(cancellationToken);

            var firstMunicipal = await db.Offices.Where(o => o.Kind == OfficeKind.Municipal && o.Code.StartsWith("DEMO-MUN-") && o.Status == RecordStatus.Active)
                .OrderBy(o => o.Code).FirstOrDefaultAsync(cancellationToken);
            var roles = await db.Roles.ToDictionaryAsync(r => r.Code, cancellationToken);
            foreach (var user in users)
            {
                // "none": left to sign in as a new, pending applicant (workflow-security.md §4.2).
                if (!Guid.TryParse(user.UserId, out var supabaseId) || user.Office.Equals("none", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                var appUser = await db.AppUsers.FirstOrDefaultAsync(u => u.SupabaseUserId == supabaseId, cancellationToken);
                if (appUser is null)
                {
                    appUser = new AppUser { SupabaseUserId = supabaseId, DisplayName = user.DisplayName, Email = string.Empty, Status = AppUserStatus.Active };
                    db.AppUsers.Add(appUser);
                    await db.SaveChangesAsync(cancellationToken);
                }
                var hasAssignment = await db.OfficeAssignments.AnyAsync(a => a.AppUserId == appUser.Id && (a.Status == WorkflowStatus.Draft
                    || (a.Status == WorkflowStatus.Approved && a.EffectiveDate <= today && (a.EndDate == null || a.EndDate >= today))), cancellationToken);
                // Its own DEMO assignment in force gains roles added to the configuration since (dev users' permissions, Phase 12).
                var demo = await db.OfficeAssignments.Include(a => a.Roles).FirstOrDefaultAsync(a => a.AppUserId == appUser.Id && a.LegalBasis == LegalBasis
                    && a.Status == WorkflowStatus.Approved && a.EffectiveDate <= today && (a.EndDate == null || a.EndDate >= today), cancellationToken);
                if (demo is not null)
                {
                    var missing = user.Roles.Where(roles.ContainsKey).Select(r => roles[r].Id).Where(id => demo.Roles.All(x => x.RoleId != id)).ToList();
                    if (missing.Count > 0)
                    {
                        // Added through their own set: their keys are set on creation, so through the parent EF would update them.
                        db.OfficeAssignmentRoles.AddRange(missing.Select(id => new OfficeAssignmentRole { OfficeAssignmentId = demo.Id, RoleId = id }));
                        await db.SaveChangesAsync(cancellationToken);
                    }
                }
                // A user with an ended assignment keeps history; only someone never assigned (or no longer) gets the DEMO one.
                if (hasAssignment || await db.OfficeAssignments.AnyAsync(a => a.AppUserId == appUser.Id && a.Status == WorkflowStatus.Approved && a.EffectiveDate > today, cancellationToken))
                {
                    continue;
                }
                var office = user.Office.ToLowerInvariant() switch
                {
                    "province-wide" => null,
                    "municipal" => firstMunicipal,
                    _ => provincial,
                };
                if (user.Office.Equals("municipal", StringComparison.OrdinalIgnoreCase) && office is null)
                {
                    logger.LogWarning("DevOfficeSeeder: no DEMO municipal office exists, so {User} gets no office.", user.Key);
                    continue;
                }
                var assignment = new OfficeAssignment
                {
                    AppUserId = appUser.Id, OfficeId = office?.Id, EffectiveDate = await StartAfterHistoryAsync(db, appUser.Id, cancellationToken),
                    LegalBasis = LegalBasis, Status = WorkflowStatus.Approved, ApprovedAt = now,
                    Roles = user.Roles.Where(roles.ContainsKey).Select(r => new OfficeAssignmentRole { RoleId = roles[r].Id }).ToList(),
                };
                db.OfficeAssignments.Add(assignment);
                await db.SaveChangesAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            // Development convenience only: never block start-up. A concurrent start (several test hosts)
            // may race on the unique indexes, or the database may not be migrated yet.
            logger.LogWarning(ex, "DevOfficeSeeder did not finish; DEMO offices may be missing.");
        }
    }

    /// <summary>A new DEMO assignment starts after any earlier one ended, keeping one assignment per user at a time.</summary>
    private static async Task<DateOnly> StartAfterHistoryAsync(PrimeDbContext db, Guid userId, CancellationToken ct)
    {
        var lastEnd = await db.OfficeAssignments.Where(a => a.AppUserId == userId && a.Status == WorkflowStatus.Approved)
            .MaxAsync(a => (DateOnly?)a.EndDate, ct);
        return lastEnd is { } end ? end.AddDays(1) : From;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
