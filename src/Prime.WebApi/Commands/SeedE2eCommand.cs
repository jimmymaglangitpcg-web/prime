using Microsoft.EntityFrameworkCore;
using Npgsql;
using Prime.Domain.Entities;
using Prime.Domain.Entities.Reference;
using Prime.Domain.Enums;
using Prime.Infrastructure.Persistence;

namespace Prime.WebApi.Commands;

/// <summary>
/// <c>dotnet run --project src/Prime.WebApi -- seed-e2e</c>: the small DEMO set the browser end-to-end suite works on
/// (frontend/prime-web/e2e; docs/analysis/production-hardening.md §4.7, Q8). Refused unless the environment is Development
/// and the database is on this machine. It runs before the API starts, so the development office seeder gives the new
/// DEMO municipalities their offices at the next start.
/// <para>
/// Idempotent: it adds only what is missing, found by code. It writes a DEMO province with two municipalities of one
/// barangay each, one land classification and actual use, a sole-owner ownership type, and an approved SMV covering both
/// municipalities (1,000 per sqm) and assessment level (20 %), effective from 2020. The names are distinct, so a test
/// picks them on screen without ambiguity. Every name and value is DEMO, not LGU data (CLAUDE.md §81).
/// </para>
/// </summary>
public static class SeedE2eCommand
{
    public const string Name = "seed-e2e";
    public const string Prefix = "E2E";
    private static readonly DateOnly Effective = new(2020, 1, 1);

    public static async Task<int> RunAsync(IConfiguration configuration, IHostEnvironment environment)
    {
        var connectionString = configuration.GetConnectionString("PrimeDb") ?? "";
        var target = new NpgsqlConnectionStringBuilder(connectionString);
        if (!environment.IsDevelopment() || target.Host is not ("localhost" or "127.0.0.1" or "::1"))
        {
            Console.Error.WriteLine("Refused: seed-e2e runs only in Development, against a database on this machine.");
            return 1;
        }
        var options = new DbContextOptionsBuilder<PrimeDbContext>().UseNpgsql(connectionString, o => o.UseNetTopologySuite()).Options;
        await using var db = new PrimeDbContext(options);
        var now = DateTimeOffset.UtcNow;

        var province = await FindOrAddAsync(db, x => x.PsgcCode == $"{Prefix}-PROV",
            () => new Province { PsgcCode = $"{Prefix}-PROV", Name = "DEMO E2E Province" });
        var home = await FindOrAddAsync(db, x => x.PsgcCode == $"{Prefix}-MUN-1",
            () => new Municipality { Province = province, PsgcCode = $"{Prefix}-MUN-1", Name = "DEMO E2E Municipality" });
        var other = await FindOrAddAsync(db, x => x.PsgcCode == $"{Prefix}-MUN-2",
            () => new Municipality { Province = province, PsgcCode = $"{Prefix}-MUN-2", Name = "DEMO E2E Other Municipality" });
        await FindOrAddAsync(db, x => x.PsgcCode == $"{Prefix}-BRGY-1",
            () => new Barangay { Municipality = home, PsgcCode = $"{Prefix}-BRGY-1", Name = "DEMO E2E Barangay" });
        await FindOrAddAsync(db, x => x.PsgcCode == $"{Prefix}-BRGY-2",
            () => new Barangay { Municipality = other, PsgcCode = $"{Prefix}-BRGY-2", Name = "DEMO E2E Other Barangay" });
        var classification = await FindOrAddAsync(db, x => x.Code == $"{Prefix}-RES",
            () => new Classification { Code = $"{Prefix}-RES", Name = "DEMO E2E Residential" });
        var use = await FindOrAddAsync(db, x => x.Code == $"{Prefix}-RES",
            () => new ActualUse { Code = $"{Prefix}-RES", Name = "DEMO E2E Residential use" });
        await FindOrAddAsync(db, x => x.Code == $"{Prefix}-SOLE",
            () => new OwnershipType { Code = $"{Prefix}-SOLE", Name = "DEMO E2E Sole owner" });
        // Property types are looked up by code by the services, so an existing one is reused.
        var land = await FindOrAddAsync(db, x => x.Code == "LAND", () => new PropertyType { Code = "LAND", Name = "DEMO_Land" });
        await db.SaveChangesAsync();

        if (!await db.Smvs.AnyAsync(x => x.OrdinanceNumber == $"DEMO-{Prefix}-SMV"))
        {
            var smv = new Smv
            {
                Basis = SmvBasis.Ordinance, OrdinanceNumber = $"DEMO-{Prefix}-SMV", OrdinanceDate = Effective.AddMonths(-1),
                ApprovalDate = Effective.AddMonths(-1), EffectivityDate = Effective, RevisionYear = Effective.Year,
                Status = WorkflowStatus.Approved, ApprovedAt = now, Description = "DEMO E2E SMV for the browser tests; not an ordinance.",
                Coverage = [new SmvCoverage { MunicipalityId = home.Id }, new SmvCoverage { MunicipalityId = other.Id }],
            };
            db.Add(smv);
            db.Add(new SmvSchedule
            {
                SmvId = smv.Id, ClassificationId = classification.Id, ActualUseId = use.Id, PropertyTypeId = land.Id, Unit = "per sqm",
                MarketValue = 1_000m, EffectiveDate = Effective, Status = WorkflowStatus.Approved, ApprovedAt = now,
            });
        }
        if (!await db.AssessmentLevels.AnyAsync(x => x.OrdinanceNumber == $"DEMO-{Prefix}-AL"))
        {
            db.Add(new AssessmentLevel
            {
                OrdinanceNumber = $"DEMO-{Prefix}-AL", OrdinanceDate = Effective.AddMonths(-1), ClassificationId = classification.Id,
                ActualUseId = use.Id, PropertyTypeId = land.Id, LowerValue = 0m, UpperValue = null, AssessmentPercentage = 20m,
                EffectiveDate = Effective, Status = WorkflowStatus.Approved, ApprovedAt = now,
            });
        }
        await db.SaveChangesAsync();
        Console.WriteLine("DEMO E2E set in place.");
        return 0;
    }

    private static async Task<T> FindOrAddAsync<T>(PrimeDbContext db, System.Linq.Expressions.Expression<Func<T, bool>> match, Func<T> create)
        where T : class
    {
        if (await db.Set<T>().FirstOrDefaultAsync(match) is { } existing)
        {
            return existing;
        }
        var created = create();
        db.Add(created);
        return created;
    }
}
