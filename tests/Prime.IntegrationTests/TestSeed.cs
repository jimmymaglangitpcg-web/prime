using Microsoft.EntityFrameworkCore;
using Prime.Domain.Entities.Reference;
using Prime.Infrastructure.Persistence;

namespace Prime.IntegrationTests;

/// <summary>Seeding helpers shared by the flow tests, which run against the (possibly already populated) dev database.</summary>
internal static class TestSeed
{
    /// <summary>
    /// The LAND property type, reused when the database already has one:
    /// services look it up by code, so any usable database carries exactly
    /// one and a test must not insert a second (IX_PropertyTypes_Code).
    /// </summary>
    public static async Task<PropertyType> LandPropertyTypeAsync(PrimeDbContext db)
    {
        var existing = await db.PropertyTypes.FirstOrDefaultAsync(x => x.Code == "LAND");
        if (existing is not null)
        {
            return existing;
        }
        var created = new PropertyType { Code = "LAND", Name = "DEMO_Land" };
        db.PropertyTypes.Add(created);
        return created;
    }
}
