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
    public DbSet<Prime.Domain.Entities.SwornStatements.SwornStatement> SwornStatements => Set<Prime.Domain.Entities.SwornStatements.SwornStatement>();
    public DbSet<Prime.Domain.Entities.SwornStatements.SwornStatementItem> SwornStatementItems => Set<Prime.Domain.Entities.SwornStatements.SwornStatementItem>();
    public DbSet<LandImprovement> LandImprovements => Set<LandImprovement>();
    public DbSet<LandAdjustment> LandAdjustments => Set<LandAdjustment>();
    public DbSet<AdjustmentFactor> AdjustmentFactors => Set<AdjustmentFactor>();
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
    public DbSet<NumberingScheme> NumberingSchemes => Set<NumberingScheme>();
    public DbSet<NumberSequence> NumberSequences => Set<NumberSequence>();
    public DbSet<FormDefinition> FormDefinitions => Set<FormDefinition>();
    public DbSet<IssuedForm> IssuedForms => Set<IssuedForm>();
    public DbSet<ApprovalChain> ApprovalChains => Set<ApprovalChain>();
    public DbSet<ApprovalChainStep> ApprovalChainSteps => Set<ApprovalChainStep>();
    public DbSet<ApprovalRecord> ApprovalRecords => Set<ApprovalRecord>();
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

        base.OnModelCreating(modelBuilder);
    }
}
