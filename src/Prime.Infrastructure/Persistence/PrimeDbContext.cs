using Microsoft.EntityFrameworkCore;
using Prime.Application.Common.Interfaces;
using Prime.Domain.Entities;
using Prime.Domain.Entities.Billing;
using Prime.Domain.Entities.Forms;
using Prime.Domain.Entities.Notices;
using Prime.Domain.Entities.Transactions;
using Prime.Domain.Entities.Workflow;
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
public class PrimeDbContext(DbContextOptions<PrimeDbContext> options, Prime.Infrastructure.Identity.JurisdictionState? jurisdiction = null)
    : DbContext(options), IApplicationDbContext
{
    // Read by the jurisdiction query filters for every query (EF evaluates context members per query).
    private bool JurisdictionRestricted => jurisdiction?.Restricted ?? false;
    private List<Guid> JurisdictionMunicipalities => jurisdiction?.FilterIds ?? [];

    // Reference / lookup data (CLAUDE.md §27)
    public DbSet<Province> Provinces => Set<Province>();
    public DbSet<Municipality> Municipalities => Set<Municipality>();
    public DbSet<Barangay> Barangays => Set<Barangay>();
    public DbSet<CityDistrict> CityDistricts => Set<CityDistrict>();
    public DbSet<TaxMapSection> TaxMapSections => Set<TaxMapSection>();
    public DbSet<PinAssignment> PinAssignments => Set<PinAssignment>();
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
    public DbSet<TaxType> TaxTypes => Set<TaxType>();
    public DbSet<ImprovementKind> ImprovementKinds => Set<ImprovementKind>();
    public DbSet<LandStrip> LandStrips => Set<LandStrip>();
    public DbSet<BuildingUsePortion> BuildingUsePortions => Set<BuildingUsePortion>();
    public DbSet<BuildingFloor> BuildingFloors => Set<BuildingFloor>();
    public DbSet<BuildingMaterial> BuildingMaterials => Set<BuildingMaterial>();
    public DbSet<TitleType> TitleTypes => Set<TitleType>();
    public DbSet<StructuralPart> StructuralParts => Set<StructuralPart>();
    public DbSet<StructuralMaterial> StructuralMaterials => Set<StructuralMaterial>();
    public DbSet<TransferTaxClearance> TransferTaxClearances => Set<TransferTaxClearance>();
    public DbSet<Prime.Domain.Entities.Registers.RegisterRun> RegisterRuns => Set<Prime.Domain.Entities.Registers.RegisterRun>();
    public DbSet<Prime.Domain.Entities.Registers.AssessmentRollSubmission> AssessmentRollSubmissions => Set<Prime.Domain.Entities.Registers.AssessmentRollSubmission>();
    public DbSet<Prime.Domain.Entities.Registers.AssessmentRollSubmissionItem> AssessmentRollSubmissionItems => Set<Prime.Domain.Entities.Registers.AssessmentRollSubmissionItem>();
    public DbSet<Prime.Domain.Entities.SwornStatements.SwornStatement> SwornStatements => Set<Prime.Domain.Entities.SwornStatements.SwornStatement>();
    public DbSet<Prime.Domain.Entities.SwornStatements.SwornStatementItem> SwornStatementItems => Set<Prime.Domain.Entities.SwornStatements.SwornStatementItem>();
    public DbSet<LandImprovement> LandImprovements => Set<LandImprovement>();
    public DbSet<LandAdjustment> LandAdjustments => Set<LandAdjustment>();
    public DbSet<AdjustmentFactor> AdjustmentFactors => Set<AdjustmentFactor>();
    public DbSet<SmvBuildingCost> SmvBuildingCosts => Set<SmvBuildingCost>();
    public DbSet<SmvExtraItemCost> SmvExtraItemCosts => Set<SmvExtraItemCost>();
    public DbSet<SmvDepreciationSchedule> SmvDepreciationSchedules => Set<SmvDepreciationSchedule>();
    public DbSet<MachineryCostItem> MachineryCostItems => Set<MachineryCostItem>();
    public DbSet<ExchangeRate> ExchangeRates => Set<ExchangeRate>();
    public DbSet<PriceIndex> PriceIndices => Set<PriceIndex>();
    public DbSet<IndependentAppraisal> IndependentAppraisals => Set<IndependentAppraisal>();
    public DbSet<BackTaxRun> BackTaxRuns => Set<BackTaxRun>();

    public void ClearChangeTracker() => ChangeTracker.Clear();
    public DbSet<BackTaxPeriod> BackTaxPeriods => Set<BackTaxPeriod>();
    public DbSet<TerritorialChangeJob> TerritorialChangeJobs => Set<TerritorialChangeJob>();
    public DbSet<TerritorialChangeItem> TerritorialChangeItems => Set<TerritorialChangeItem>();
    public DbSet<PropertyBarangayPart> PropertyBarangayParts => Set<PropertyBarangayPart>();
    public DbSet<Prime.Domain.Entities.Gis.DisputedArea> DisputedAreas => Set<Prime.Domain.Entities.Gis.DisputedArea>();
    public DbSet<TaxRate> TaxRates => Set<TaxRate>();
    public DbSet<PaymentSchedule> PaymentSchedules => Set<PaymentSchedule>();
    public DbSet<DiscountRule> DiscountRules => Set<DiscountRule>();
    public DbSet<InterestRule> InterestRules => Set<InterestRule>();
    public DbSet<PenaltyRule> PenaltyRules => Set<PenaltyRule>();
    public DbSet<TaxIncreaseCapRule> TaxIncreaseCapRules => Set<TaxIncreaseCapRule>();
    public DbSet<TaxBill> TaxBills => Set<TaxBill>();
    public DbSet<TaxBillTaxType> TaxBillTaxTypes => Set<TaxBillTaxType>();
    public DbSet<TaxBillTaxTypeLine> TaxBillTaxTypeLines => Set<TaxBillTaxTypeLine>();
    public DbSet<TaxBillDetail> TaxBillDetails => Set<TaxBillDetail>();
    public DbSet<Prime.Domain.Entities.Collection.Payment> Payments => Set<Prime.Domain.Entities.Collection.Payment>();
    public DbSet<Prime.Domain.Entities.Collection.PaymentTender> PaymentTenders => Set<Prime.Domain.Entities.Collection.PaymentTender>();
    public DbSet<Prime.Domain.Entities.Collection.PaymentAllocation> PaymentAllocations => Set<Prime.Domain.Entities.Collection.PaymentAllocation>();
    public DbSet<Prime.Domain.Entities.Collection.PaymentCancellation> PaymentCancellations => Set<Prime.Domain.Entities.Collection.PaymentCancellation>();
    public DbSet<Prime.Domain.Entities.Collection.Remittance> Remittances => Set<Prime.Domain.Entities.Collection.Remittance>();
    public DbSet<Prime.Domain.Entities.Collection.RemittanceItem> RemittanceItems => Set<Prime.Domain.Entities.Collection.RemittanceItem>();
    public DbSet<Prime.Domain.Entities.Collection.RemittanceModeTotal> RemittanceModeTotals => Set<Prime.Domain.Entities.Collection.RemittanceModeTotal>();
    public DbSet<Prime.Domain.Entities.Collection.RemittanceAccountTotal> RemittanceAccountTotals => Set<Prime.Domain.Entities.Collection.RemittanceAccountTotal>();
    public DbSet<Prime.Domain.Entities.Collection.PaymentMode> PaymentModes => Set<Prime.Domain.Entities.Collection.PaymentMode>();
    public DbSet<Prime.Domain.Entities.Collection.RevenueAccountMapping> RevenueAccountMappings => Set<Prime.Domain.Entities.Collection.RevenueAccountMapping>();
    public DbSet<NumberingScheme> NumberingSchemes => Set<NumberingScheme>();
    public DbSet<NumberSequence> NumberSequences => Set<NumberSequence>();
    public DbSet<FormDefinition> FormDefinitions => Set<FormDefinition>();
    public DbSet<IssuedForm> IssuedForms => Set<IssuedForm>();
    public DbSet<ApprovalChain> ApprovalChains => Set<ApprovalChain>();
    public DbSet<ApprovalChainStep> ApprovalChainSteps => Set<ApprovalChainStep>();
    public DbSet<ApprovalRecord> ApprovalRecords => Set<ApprovalRecord>();
    public DbSet<Prime.Domain.Entities.Content.ContentImport> ContentImports => Set<Prime.Domain.Entities.Content.ContentImport>();
    public DbSet<Prime.Domain.Entities.Content.ContentImportItem> ContentImportItems => Set<Prime.Domain.Entities.Content.ContentImportItem>();
    public DbSet<TransactionType> TransactionTypes => Set<TransactionType>();
    public DbSet<TransactionTypeRequirement> TransactionTypeRequirements => Set<TransactionTypeRequirement>();
    public DbSet<PropertyTransaction> PropertyTransactions => Set<PropertyTransaction>();
    public DbSet<PropertyTransactionRequirement> PropertyTransactionRequirements => Set<PropertyTransactionRequirement>();
    public DbSet<PropertyTransactionParty> PropertyTransactionParties => Set<PropertyTransactionParty>();
    public DbSet<PropertyTransactionTdCancellation> PropertyTransactionTdCancellations => Set<PropertyTransactionTdCancellation>();
    public DbSet<PropertyTransactionProperty> PropertyTransactionProperties => Set<PropertyTransactionProperty>();
    public DbSet<NoticeOfAssessment> NoticesOfAssessment => Set<NoticeOfAssessment>();
    public DbSet<NoticeOfAssessmentItem> NoticeOfAssessmentItems => Set<NoticeOfAssessmentItem>();
    public DbSet<BarangayBoundary> BarangayBoundaries => Set<BarangayBoundary>();
    public DbSet<SectionBoundary> SectionBoundaries => Set<SectionBoundary>();
    public DbSet<ZoneBoundary> ZoneBoundaries => Set<ZoneBoundary>();
    public DbSet<RoadSegment> RoadSegments => Set<RoadSegment>();
    public DbSet<RealPropertyUnit> RealPropertyUnits => Set<RealPropertyUnit>();
    public DbSet<TaxDeclaration> TaxDeclarations => Set<TaxDeclaration>();
    public DbSet<TaxDeclarationAnnotation> TaxDeclarationAnnotations => Set<TaxDeclarationAnnotation>();
    public DbSet<AnnotationType> AnnotationTypes => Set<AnnotationType>();
    public DbSet<Land> Lands => Set<Land>();
    public DbSet<Building> Buildings => Set<Building>();
    public DbSet<BuildingComponent> BuildingComponents => Set<BuildingComponent>();
    public DbSet<Machinery> MachineryUnits => Set<Machinery>();

    // Valuation (CLAUDE.md §28-31)
    public DbSet<Smv> Smvs => Set<Smv>();
    public DbSet<SmvSchedule> SmvSchedules => Set<SmvSchedule>();
    public DbSet<AssessmentLevel> AssessmentLevels => Set<AssessmentLevel>();
    public DbSet<Valuation> Valuations => Set<Valuation>();
    public DbSet<ValuationLine> ValuationLines => Set<ValuationLine>();
    public DbSet<Assessment> Assessments => Set<Assessment>();
    public DbSet<AssessmentLine> AssessmentLines => Set<AssessmentLine>();
    public DbSet<GeneralRevisionJob> GeneralRevisionJobs => Set<GeneralRevisionJob>();

    // Identity / authorization (CLAUDE.md §47; Supabase Auth owns credentials)
    public DbSet<AppUser> AppUsers => Set<AppUser>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();

    // Offices and jurisdiction (CLAUDE.md §117; docs/analysis/province-wide-operation.md)
    public DbSet<Prime.Domain.Entities.Offices.Office> Offices => Set<Prime.Domain.Entities.Offices.Office>();
    public DbSet<Prime.Domain.Entities.Offices.OfficeJurisdiction> OfficeJurisdictions => Set<Prime.Domain.Entities.Offices.OfficeJurisdiction>();
    public DbSet<Prime.Domain.Entities.Offices.OfficeAssignment> OfficeAssignments => Set<Prime.Domain.Entities.Offices.OfficeAssignment>();
    public DbSet<Prime.Domain.Entities.Offices.OfficeAssignmentRole> OfficeAssignmentRoles => Set<Prime.Domain.Entities.Offices.OfficeAssignmentRole>();
    public DbSet<Prime.Domain.Entities.Offices.ApprovalDelegation> ApprovalDelegations => Set<Prime.Domain.Entities.Offices.ApprovalDelegation>();

    // Documents (CLAUDE.md §59; Supabase Storage backed)
    public DbSet<Document> Documents => Set<Document>();

    // Audit trail (CLAUDE.md §48) — written only by AuditSaveChangesInterceptor.
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("postgis");
        // Exclusion constraint over uuid/text + daterange on TaxIncreaseCapRules.
        modelBuilder.HasPostgresExtension("btree_gist");

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PrimeDbContext).Assembly);
        ApplyJurisdictionFilters(modelBuilder);

        base.OnModelCreating(modelBuilder);
    }

    /// <summary>
    /// Records belong to a municipality (docs/analysis/province-wide-operation.md §3.3, Q3):
    /// the property's, for everything that hangs off a property. A restricted
    /// request sees only its municipalities; everyone else sees all. Uniqueness
    /// checks that must see the whole province use <c>IgnoreQueryFilters()</c>.
    /// The frozen treasury tables are not filtered (CLAUDE.md §0).
    /// </summary>
    private void ApplyJurisdictionFilters(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PropertyEntity>().HasQueryFilter(x => !JurisdictionRestricted || JurisdictionMunicipalities.Contains(x.MunicipalityId));
        modelBuilder.Entity<RealPropertyUnit>().HasQueryFilter(x => !JurisdictionRestricted || JurisdictionMunicipalities.Contains(x.Property!.MunicipalityId));
        modelBuilder.Entity<Parcel>().HasQueryFilter(x => !JurisdictionRestricted || JurisdictionMunicipalities.Contains(x.Property!.MunicipalityId));
        modelBuilder.Entity<Land>().HasQueryFilter(x => !JurisdictionRestricted || JurisdictionMunicipalities.Contains(x.Property!.MunicipalityId));
        modelBuilder.Entity<Building>().HasQueryFilter(x => !JurisdictionRestricted || JurisdictionMunicipalities.Contains(x.Property!.MunicipalityId));
        modelBuilder.Entity<Machinery>().HasQueryFilter(x => !JurisdictionRestricted || JurisdictionMunicipalities.Contains(x.Property!.MunicipalityId));
        modelBuilder.Entity<PropertyTaxpayer>().HasQueryFilter(x => !JurisdictionRestricted || JurisdictionMunicipalities.Contains(x.Property!.MunicipalityId));
        modelBuilder.Entity<PinAssignment>().HasQueryFilter(x => !JurisdictionRestricted || JurisdictionMunicipalities.Contains(x.Property!.MunicipalityId));
        modelBuilder.Entity<PropertyBarangayPart>().HasQueryFilter(x => !JurisdictionRestricted || JurisdictionMunicipalities.Contains(x.Property!.MunicipalityId));
        modelBuilder.Entity<Valuation>().HasQueryFilter(x => !JurisdictionRestricted || JurisdictionMunicipalities.Contains(x.Property!.MunicipalityId));
        modelBuilder.Entity<Assessment>().HasQueryFilter(x => !JurisdictionRestricted || JurisdictionMunicipalities.Contains(x.Property!.MunicipalityId));
        modelBuilder.Entity<TaxDeclaration>().HasQueryFilter(x => !JurisdictionRestricted || JurisdictionMunicipalities.Contains(x.Property!.MunicipalityId));
        modelBuilder.Entity<NoticeOfAssessment>().HasQueryFilter(x => !JurisdictionRestricted || JurisdictionMunicipalities.Contains(x.Property!.MunicipalityId));
        // No navigation to its property: look the property up (a notice's items belong to its own municipality).
        modelBuilder.Entity<NoticeOfAssessmentItem>().HasQueryFilter(x => !JurisdictionRestricted
            || Set<PropertyEntity>().Any(p => p.Id == x.PropertyId && JurisdictionMunicipalities.Contains(p.MunicipalityId)));
        modelBuilder.Entity<PropertyTransaction>().HasQueryFilter(x => !JurisdictionRestricted || JurisdictionMunicipalities.Contains(x.Property!.MunicipalityId));
        modelBuilder.Entity<Prime.Domain.Entities.SwornStatements.SwornStatement>()
            .HasQueryFilter(x => !JurisdictionRestricted || JurisdictionMunicipalities.Contains(x.MunicipalityId));
        modelBuilder.Entity<Prime.Domain.Entities.Registers.RegisterRun>()
            .HasQueryFilter(x => !JurisdictionRestricted || x.BarangayId == null || JurisdictionMunicipalities.Contains(x.Barangay!.MunicipalityId));
        modelBuilder.Entity<Prime.Domain.Entities.Registers.AssessmentRollSubmission>()
            .HasQueryFilter(x => !JurisdictionRestricted || JurisdictionMunicipalities.Contains(x.MunicipalityId));
    }
}
