using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using NetTopologySuite;
using NetTopologySuite.Geometries;
using Prime.Domain.Entities;
using Prime.Domain.Entities.Reference;
using Prime.Domain.Enums;
using Prime.Infrastructure.Persistence;
using Shouldly;
using Xunit;

namespace Prime.IntegrationTests.Persistence;

/// <summary>
/// Exercises the Phase 3 schema against the real local PostgreSQL/PostGIS
/// database (via the same DI-resolved PrimeDbContext the app uses) to prove
/// the constraints and indexes declared in the EF configurations actually
/// take effect in the database — not just that the migration "ran" (a
/// migration can apply without every constraint firing as intended).
/// Each test runs inside a transaction that is rolled back, never
/// committed, so the dev database is never polluted by test runs.
/// </summary>
public class ConstraintTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private async Task<(PrimeDbContext Db, IAsyncDisposable Transaction)> BeginTestTransactionAsync()
    {
        var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PrimeDbContext>();
        var transaction = await db.Database.BeginTransactionAsync();
        return (db, new ScopedTransaction(transaction, scope));
    }

    private sealed class ScopedTransaction(IDbContextTransaction transaction, IServiceScope scope) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            // Never committed — this is how each test avoids polluting the
            // real dev database.
            await transaction.DisposeAsync();
            scope.Dispose();
        }
    }

    private static async Task<(Province Province, Municipality Municipality, Barangay Barangay)> SeedGeographyAsync(PrimeDbContext db)
    {
        var province = new Province { PsgcCode = $"P{Guid.NewGuid():N}"[..10], Name = "Demo Province" };
        var municipality = new Municipality { Province = province, PsgcCode = $"M{Guid.NewGuid():N}"[..10], Name = "Demo Municipality" };
        var barangay = new Barangay { Municipality = municipality, PsgcCode = $"B{Guid.NewGuid():N}"[..10], Name = "Demo Barangay" };

        db.Provinces.Add(province);
        db.Municipalities.Add(municipality);
        db.Barangays.Add(barangay);
        await db.SaveChangesAsync();

        return (province, municipality, barangay);
    }

    [Fact]
    public async Task Property_DuplicatePropertyIdentificationNumber_ViolatesUniqueConstraint()
    {
        var (db, transaction) = await BeginTestTransactionAsync();
        await using var _ = transaction;

        var (province, municipality, barangay) = await SeedGeographyAsync(db);
        var pin = $"PIN-{Guid.NewGuid():N}";

        db.Properties.Add(new PropertyEntity
        {
            PropertyIdentificationNumber = pin,
            Province = province,
            Municipality = municipality,
            Barangay = barangay,
        });
        await db.SaveChangesAsync();

        db.Properties.Add(new PropertyEntity
        {
            PropertyIdentificationNumber = pin,
            Province = province,
            Municipality = municipality,
            Barangay = barangay,
        });

        await Should.ThrowAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Province_CannotBeDeleted_WhileMunicipalityReferencesIt()
    {
        var (db, transaction) = await BeginTestTransactionAsync();
        await using var _ = transaction;

        var (province, _, _) = await SeedGeographyAsync(db);

        // FK configured with DeleteBehavior.Restrict (docs/DATABASE.md §5 —
        // history-bearing/reference rows are never allowed to cascade-delete).
        // With the dependent Municipality still tracked in this same
        // DbContext (from SeedGeographyAsync), EF Core's own change tracker
        // detects the non-nullable FK would be severed and throws
        // InvalidOperationException synchronously from Remove() itself
        // (client-side cascade-delete fixup runs immediately, before
        // SaveChanges is even called) — a fail-fast property of Restrict +
        // a required FK, not a weaker guarantee than the DB-level
        // constraint (which still exists as a second line of defense for
        // any path that bypasses EF's tracked graph, e.g. raw SQL — see
        // the next test).
        Should.Throw<InvalidOperationException>(() => db.Provinces.Remove(province));
    }

    [Fact]
    public async Task Province_CannotBeDeleted_ViaRawSql_ProvingTheDbLevelConstraintItself()
    {
        // Complements the test above: bypasses EF's change tracker
        // entirely (fresh untracked context path via raw SQL) to prove the
        // RESTRICT foreign key constraint exists in the database itself,
        // not only as an EF client-side convenience check.
        var (db, transaction) = await BeginTestTransactionAsync();
        await using var _ = transaction;

        // Filtered to the specific province just seeded — other
        // (committed) test data can coexist in this table, e.g. from
        // PropertyRegistrationFlowTests, so the table is never assumed to
        // contain exactly one row.
        var (province, _, _) = await SeedGeographyAsync(db);
        var provinceId = province.Id;

        var ex = await Should.ThrowAsync<Npgsql.PostgresException>(() =>
            db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM \"Provinces\" WHERE \"Id\" = {provinceId}"));

        ex.SqlState.ShouldBe("23503"); // foreign_key_violation
    }

    [Fact]
    public async Task RealPropertyUnit_DuplicateRpuNumber_ViolatesUniqueConstraint()
    {
        var (db, transaction) = await BeginTestTransactionAsync();
        await using var _ = transaction;

        var (province, municipality, barangay) = await SeedGeographyAsync(db);
        var property = new PropertyEntity
        {
            PropertyIdentificationNumber = $"PIN-{Guid.NewGuid():N}",
            Province = province,
            Municipality = municipality,
            Barangay = barangay,
        };
        db.Properties.Add(property);
        await db.SaveChangesAsync();

        var rpuNumber = $"RPU-{Guid.NewGuid():N}";
        db.RealPropertyUnits.Add(new RealPropertyUnit
        {
            Property = property,
            RpuNumber = rpuNumber,
            RpuType = RpuType.Land,
            EffectivityDate = DateOnly.FromDateTime(DateTime.UtcNow),
        });
        await db.SaveChangesAsync();

        db.RealPropertyUnits.Add(new RealPropertyUnit
        {
            Property = property,
            RpuNumber = rpuNumber,
            RpuType = RpuType.Land,
            EffectivityDate = DateOnly.FromDateTime(DateTime.UtcNow),
        });

        await Should.ThrowAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Parcel_Geometry_RoundTripsThroughRealPostGis()
    {
        var (db, transaction) = await BeginTestTransactionAsync();
        await using var _ = transaction;

        var (province, municipality, barangay) = await SeedGeographyAsync(db);
        var property = new PropertyEntity
        {
            PropertyIdentificationNumber = $"PIN-{Guid.NewGuid():N}",
            Province = province,
            Municipality = municipality,
            Barangay = barangay,
        };
        db.Properties.Add(property);

        var geometryFactory = NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);
        var polygon = geometryFactory.CreatePolygon([
            new Coordinate(121.0, 14.5),
            new Coordinate(121.001, 14.5),
            new Coordinate(121.001, 14.501),
            new Coordinate(121.0, 14.501),
            new Coordinate(121.0, 14.5),
        ]);

        var parcel = new Parcel
        {
            Property = property,
            Geometry = polygon,
            Barangay = barangay,
        };
        db.Parcels.Add(parcel);
        await db.SaveChangesAsync();

        db.ChangeTracker.Clear();
        var reloaded = await db.Parcels.SingleAsync(p => p.Id == parcel.Id);

        reloaded.Geometry.ShouldNotBeNull();
        reloaded.Geometry.SRID.ShouldBe(4326);
        reloaded.Geometry.Area.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task TaxDeclaration_SupersessionChain_PreservesPreviousDeclaration()
    {
        var (db, transaction) = await BeginTestTransactionAsync();
        await using var _ = transaction;

        var (province, municipality, barangay) = await SeedGeographyAsync(db);
        var classification = new Classification { Code = $"CL{Guid.NewGuid():N}"[..8], Name = "Demo Classification" };
        var actualUse = new ActualUse { Code = $"AU{Guid.NewGuid():N}"[..8], Name = "Demo Actual Use" };
        db.Classifications.Add(classification);
        db.ActualUses.Add(actualUse);

        var property = new PropertyEntity
        {
            PropertyIdentificationNumber = $"PIN-{Guid.NewGuid():N}",
            Province = province,
            Municipality = municipality,
            Barangay = barangay,
        };
        db.Properties.Add(property);

        var rpu = new RealPropertyUnit
        {
            Property = property,
            RpuNumber = $"RPU-{Guid.NewGuid():N}",
            RpuType = RpuType.Land,
            EffectivityDate = DateOnly.FromDateTime(DateTime.UtcNow),
        };
        db.RealPropertyUnits.Add(rpu);
        await db.SaveChangesAsync();

        var original = new TaxDeclaration
        {
            Rpu = rpu,
            Property = property,
            TaxDeclarationNumber = $"TD-{Guid.NewGuid():N}",
            RevisionNumber = 1,
            EffectivityDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Classification = classification,
            ActualUse = actualUse,
            AssessmentYear = 2026,
        };
        db.TaxDeclarations.Add(original);
        await db.SaveChangesAsync();

        var superseding = new TaxDeclaration
        {
            Rpu = rpu,
            Property = property,
            TaxDeclarationNumber = $"TD-{Guid.NewGuid():N}",
            RevisionNumber = 2,
            EffectivityDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Classification = classification,
            ActualUse = actualUse,
            AssessmentYear = 2027,
            PreviousTaxDeclaration = original,
        };
        db.TaxDeclarations.Add(superseding);
        await db.SaveChangesAsync();

        db.ChangeTracker.Clear();
        var reloaded = await db.TaxDeclarations
            .Include(td => td.PreviousTaxDeclaration)
            .SingleAsync(td => td.Id == superseding.Id);

        reloaded.PreviousTaxDeclaration.ShouldNotBeNull();
        reloaded.PreviousTaxDeclaration!.Id.ShouldBe(original.Id);
        // The original row must still exist, untouched — never overwritten.
        (await db.TaxDeclarations.CountAsync(td => td.Id == original.Id)).ShouldBe(1);
    }
}
