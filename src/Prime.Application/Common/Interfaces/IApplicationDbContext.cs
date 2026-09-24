using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Prime.Domain.Entities;
using Prime.Domain.Entities.Billing;
using Prime.Domain.Entities.Forms;
using Prime.Domain.Entities.Workflow;
using Prime.Domain.Entities.Gis;
using Prime.Domain.Entities.Identity;
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
    DbSet<TaxType> TaxTypes { get; }
    DbSet<TaxRate> TaxRates { get; }
    DbSet<PaymentSchedule> PaymentSchedules { get; }
    DbSet<DiscountRule> DiscountRules { get; }
    DbSet<InterestRule> InterestRules { get; }
    DbSet<PenaltyRule> PenaltyRules { get; }
    DbSet<TaxIncreaseCapRule> TaxIncreaseCapRules { get; }
    DbSet<TaxBill> TaxBills { get; }
    DbSet<TaxBillTaxType> TaxBillTaxTypes { get; }
    DbSet<TaxBillDetail> TaxBillDetails { get; }
    DbSet<NumberingScheme> NumberingSchemes { get; }
    DbSet<NumberSequence> NumberSequences { get; }
    DbSet<FormDefinition> FormDefinitions { get; }
    DbSet<IssuedForm> IssuedForms { get; }
    DbSet<ApprovalChain> ApprovalChains { get; }
    DbSet<ApprovalChainStep> ApprovalChainSteps { get; }
    DbSet<ApprovalRecord> ApprovalRecords { get; }
    /// <summary>Read by forms and approvals for signatory names.</summary>
    DbSet<AppUser> AppUsers { get; }
    DbSet<BarangayBoundary> BarangayBoundaries { get; }
    DbSet<ZoneBoundary> ZoneBoundaries { get; }
    DbSet<RoadSegment> RoadSegments { get; }
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

    /// <summary>
    /// Change-tracking access, needed to set a client-supplied concurrency
    /// token as the original value for optimistic concurrency (CLAUDE.md §66).
    /// </summary>
    EntityEntry<TEntity> Entry<TEntity>(TEntity entity) where TEntity : class;

    /// <summary>
    /// For operations that need several SaveChanges calls to be atomic
    /// (e.g. close the current boundary version, then insert its successor —
    /// the "one current version" unique index needs that order).
    /// </summary>
    DatabaseFacade Database { get; }
}
