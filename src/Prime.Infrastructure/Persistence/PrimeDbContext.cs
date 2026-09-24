using Microsoft.EntityFrameworkCore;
using Prime.Application.Common.Interfaces;
using Prime.Domain.Entities;
using Prime.Domain.Entities.Gis;
using Prime.Domain.Entities.Audit;
using Prime.Domain.Entities.Documents;
using Prime.Domain.Entities.Identity;
using Prime.Domain.Entities.Reference;

namespace Prime.Infrastructure.Persistence;

/// <summary>
/// The single EF Core DbContext for PRIME. Entity configurations are
/// discovered via <see cref="ModelBuilder.ApplyConfigurationsFromAssembly"/>
/// (Persistence/Configurations/**) rather than listed here, so adding a new
/// entity in a later phase does not require touching this file. Implements
/// <see cref="IApplicationDbContext"/> so Application handlers depend on
/// that interface, not this concrete EF Core type.
/// </summary>
public class PrimeDbContext(DbContextOptions<PrimeDbContext> options) : DbContext(options), IApplicationDbContext
{
    // Reference / lookup data (CLAUDE.md §27)
    public DbSet<Province> Provinces => Set<Province>();
    public DbSet<Municipality> Municipalities => Set<Municipality>();
    public DbSet<Barangay> Barangays => Set<Barangay>();
    public DbSet<Zone> Zones => Set<Zone>();
    public DbSet<Classification> Classifications => Set<Classification>();
    public DbSet<ActualUse> ActualUses => Set<ActualUse>();
    public DbSet<SubClassification> SubClassifications => Set<SubClassification>();
    public DbSet<RoadType> RoadTypes => Set<RoadType>();
    public DbSet<Condition> Conditions => Set<Condition>();
    public DbSet<BuildingType> BuildingTypes => Set<BuildingType>();
    public DbSet<StructuralType> StructuralTypes => Set<StructuralType>();
    public DbSet<BuildingComponentType> BuildingComponentTypes => Set<BuildingComponentType>();
    public DbSet<MachineryType> MachineryTypes => Set<MachineryType>();
    public DbSet<OwnershipType> OwnershipTypes => Set<OwnershipType>();
    public DbSet<DocumentType> DocumentTypes => Set<DocumentType>();
    public DbSet<PropertyType> PropertyTypes => Set<PropertyType>();

    // Property Registry core (CLAUDE.md §18-26)
    public DbSet<PropertyEntity> Properties => Set<PropertyEntity>();
    public DbSet<Taxpayer> Taxpayers => Set<Taxpayer>();
    public DbSet<PropertyTaxpayer> PropertyTaxpayers => Set<PropertyTaxpayer>();
    public DbSet<Parcel> Parcels => Set<Parcel>();
    public DbSet<BarangayBoundary> BarangayBoundaries => Set<BarangayBoundary>();
    public DbSet<ZoneBoundary> ZoneBoundaries => Set<ZoneBoundary>();
    public DbSet<RoadSegment> RoadSegments => Set<RoadSegment>();
    public DbSet<RealPropertyUnit> RealPropertyUnits => Set<RealPropertyUnit>();
    public DbSet<TaxDeclaration> TaxDeclarations => Set<TaxDeclaration>();
    public DbSet<Land> Lands => Set<Land>();
    public DbSet<Building> Buildings => Set<Building>();
    public DbSet<BuildingComponent> BuildingComponents => Set<BuildingComponent>();
    public DbSet<Machinery> MachineryUnits => Set<Machinery>();

    // Valuation (CLAUDE.md §28-31)
    public DbSet<Smv> Smvs => Set<Smv>();
    public DbSet<SmvSchedule> SmvSchedules => Set<SmvSchedule>();
    public DbSet<AssessmentLevel> AssessmentLevels => Set<AssessmentLevel>();
    public DbSet<Valuation> Valuations => Set<Valuation>();
    public DbSet<Assessment> Assessments => Set<Assessment>();
    public DbSet<GeneralRevisionJob> GeneralRevisionJobs => Set<GeneralRevisionJob>();

    // Identity / authorization (CLAUDE.md §47; Supabase Auth owns credentials)
    public DbSet<AppUser> AppUsers => Set<AppUser>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();

    // Documents (CLAUDE.md §59; Supabase Storage backed)
    public DbSet<Document> Documents => Set<Document>();

    // Audit trail (CLAUDE.md §48) — written only by AuditSaveChangesInterceptor.
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("postgis");

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PrimeDbContext).Assembly);

        base.OnModelCreating(modelBuilder);
    }
}
