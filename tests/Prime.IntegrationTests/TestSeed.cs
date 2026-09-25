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
}
