using Microsoft.EntityFrameworkCore;
using Prime.Domain.Entities;
using Prime.Domain.Entities.Reference;

namespace Prime.Application.Common.Interfaces;

/// <summary>
/// The persistence surface Application feature handlers are allowed to
/// see — implemented by Prime.Infrastructure's PrimeDbContext. Keeps
/// Application decoupled from EF Core's concrete DbContext type (and from
/// Infrastructure generally, per the Clean Architecture dependency
/// direction in docs/ARCHITECTURE.md §2) while still allowing direct
/// LINQ/IQueryable use against DbSet&lt;T&gt; rather than a repository
/// class per aggregate — a deliberate scope decision for Phase 4: a full
/// repository-per-aggregate layer is not justified yet.
/// </summary>
public interface IApplicationDbContext
{
    DbSet<PropertyEntity> Properties { get; }
    DbSet<Taxpayer> Taxpayers { get; }
    DbSet<PropertyTaxpayer> PropertyTaxpayers { get; }
    DbSet<Parcel> Parcels { get; }
    DbSet<RealPropertyUnit> RealPropertyUnits { get; }
    DbSet<TaxDeclaration> TaxDeclarations { get; }
    DbSet<Land> Lands { get; }
    DbSet<Building> Buildings { get; }
    DbSet<BuildingComponent> BuildingComponents { get; }
    DbSet<Machinery> MachineryUnits { get; }

    DbSet<Province> Provinces { get; }
    DbSet<Municipality> Municipalities { get; }
    DbSet<Barangay> Barangays { get; }
    DbSet<Zone> Zones { get; }
    DbSet<Classification> Classifications { get; }
    DbSet<ActualUse> ActualUses { get; }
    DbSet<SubClassification> SubClassifications { get; }
    DbSet<OwnershipType> OwnershipTypes { get; }
    DbSet<PropertyType> PropertyTypes { get; }
    DbSet<RoadType> RoadTypes { get; }
    DbSet<Condition> Conditions { get; }
    DbSet<BuildingType> BuildingTypes { get; }
    DbSet<StructuralType> StructuralTypes { get; }
    DbSet<MachineryType> MachineryTypes { get; }

    DbSet<Smv> Smvs { get; }
    DbSet<SmvSchedule> SmvSchedules { get; }
    DbSet<AssessmentLevel> AssessmentLevels { get; }
    DbSet<Valuation> Valuations { get; }
    DbSet<Assessment> Assessments { get; }
    DbSet<GeneralRevisionJob> GeneralRevisionJobs { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
