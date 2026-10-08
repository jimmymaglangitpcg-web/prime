using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Prime.Domain.Entities.Identity;
using Prime.Domain.Entities.Offices;
using Prime.Domain.Entities.Reference;
using Prime.Infrastructure.Identity;
using Prime.Infrastructure.Persistence;

using Prime.Domain.Enums;

namespace Prime.IntegrationTests;

/// <summary>Seeding helpers shared by the flow tests, which run against the (possibly already populated) dev database.</summary>
internal static class TestSeed
{
    /// <summary>A DEMO user holding <paramref name="role"/> province-wide (no office: the provincial scope); saved with the caller's next save.</summary>
    public static async Task<AppUser> UserWithRoleAsync(PrimeDbContext db, string role)
    {
        var roleId = await db.Roles.Where(r => r.Code == role).Select(r => r.Id).SingleAsync();
        var user = new AppUser { SupabaseUserId = Guid.NewGuid(), DisplayName = $"DEMO {role}", Email = $"demo-{Guid.NewGuid():N}@example.invalid" };
        db.AppUsers.Add(user);
        db.OfficeAssignments.Add(new OfficeAssignment
        {
            AppUserId = user.Id, OfficeId = null, EffectiveDate = new DateOnly(2020, 1, 1), LegalBasis = "DEMO", Status = WorkflowStatus.Approved,
            ApprovedAt = DateTimeOffset.UtcNow, Roles = [new OfficeAssignmentRole { RoleId = roleId }],
        });
        return user;
    }

    /// <summary>
    /// Runs <paramref name="decide"/> as a separate DEMO checker, then restores the acting user. Set-up code that needs
    /// approved records uses it: an approval needs a known user who did not make the record (CLAUDE.md §46;
    /// docs/analysis/workflow-security.md G7). The checker is added in the test's own transaction.
    /// </summary>
    public static async Task<T> AsCheckerAsync<T>(IServiceProvider services, Func<Task<T>> decide)
    {
        var user = services.GetRequiredService<CurrentUserService>();
        var db = services.GetRequiredService<PrimeDbContext>();
        var checker = new AppUser
        {
            SupabaseUserId = Guid.NewGuid(), DisplayName = "DEMO test checker", Email = $"demo-checker-{Guid.NewGuid():N}@example.invalid",
        };
        db.AppUsers.Add(checker);
        await db.SaveChangesAsync();
        var previous = user.AppUserId;
        user.AppUserId = checker.Id;
        try
        {
            return await decide();
        }
        finally
        {
            user.AppUserId = previous;
        }
    }

    /// <summary>
    /// The LAND property type, reused when the database already has one:
    /// services look it up by code, so any usable database carries exactly
    /// one and a test must not insert a second (IX_PropertyTypes_Code).
    /// </summary>
    public static Task<PropertyType> LandPropertyTypeAsync(PrimeDbContext db) => PropertyTypeAsync(db, "LAND", "DEMO_Land");

    /// <summary>The property type with <paramref name="code"/>, reused when present (see <see cref="LandPropertyTypeAsync"/>).</summary>
    public static async Task<PropertyType> PropertyTypeAsync(PrimeDbContext db, string code, string demoName)
    {
        var existing = await db.PropertyTypes.FirstOrDefaultAsync(x => x.Code == code);
        if (existing is not null)
        {
            return existing;
        }
        var created = new PropertyType { Code = code, Name = demoName };
        db.PropertyTypes.Add(created);
        return created;
    }

    /// <summary>
    /// Puts PRIME's built-in reference layouts (MRPAAO, provisional) back in force for the test's own transaction: the
    /// dev database may have the LAM versions approved (step L5-6, loaded from the untracked content pack), and these
    /// tests assert the reference layouts. Rolled back with the test.
    /// </summary>
    public static async Task UseReferenceFormsAsync(PrimeDbContext db)
    {
        var others = await db.FormDefinitions.Where(x => x.Status == WorkflowStatus.Approved
            && x.Authority != FormAuthority.Mrpaao && x.Authority != FormAuthority.PrimeProvisional).ToListAsync();
        // Close those first: one open approved version per code (UX_FormDefinitions_OpenApproved).
        others.ForEach(x => (x.Status, x.ApprovedAt) = (WorkflowStatus.Cancelled, null)); // CK_FormDefinitions_Approval
        await db.SaveChangesAsync();
        foreach (var code in others.Select(x => x.Code).Distinct())
        {
            var builtIn = await db.FormDefinitions.Where(x => x.Code == code && x.Status == WorkflowStatus.Approved
                    && (x.Authority == FormAuthority.Mrpaao || x.Authority == FormAuthority.PrimeProvisional))
                .OrderByDescending(x => x.Version).FirstOrDefaultAsync();
            if (builtIn is not null)
            {
                builtIn.EndDate = null;
            }
        }
        await db.SaveChangesAsync();
    }
}
