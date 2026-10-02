using Microsoft.EntityFrameworkCore;
using Prime.Domain.Entities.Reference;
using Prime.Infrastructure.Persistence;

using Prime.Domain.Enums;

namespace Prime.IntegrationTests;

/// <summary>Seeding helpers shared by the flow tests, which run against the (possibly already populated) dev database.</summary>
internal static class TestSeed
{
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
