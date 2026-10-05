using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Prime.Domain.Entities;

namespace Prime.Infrastructure.Persistence.Configurations;

/// <summary>SMV simulations (docs/analysis/smv-preparation-general-revision.md §4.3).</summary>
public sealed class SmvSimulationRunConfiguration : IEntityTypeConfiguration<SmvSimulationRun>
{
    public void Configure(EntityTypeBuilder<SmvSimulationRun> builder)
    {
        builder.ToTable("SmvSimulationRuns");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Description).HasMaxLength(1000);
        builder.Property(x => x.Remarks).HasMaxLength(1000);
        builder.HasOne(x => x.Smv).WithMany().HasForeignKey(x => x.SmvId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.Scope).WithOne().HasForeignKey(x => x.SmvSimulationRunId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => x.SmvId);
    }
}

public sealed class SmvSimulationScopeConfiguration : IEntityTypeConfiguration<SmvSimulationScope>
{
    public void Configure(EntityTypeBuilder<SmvSimulationScope> builder)
    {
        builder.ToTable("SmvSimulationScopes");
        builder.HasKey(x => x.Id);
        builder.HasOne(x => x.Municipality).WithMany().HasForeignKey(x => x.MunicipalityId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.SmvSimulationRunId, x.MunicipalityId }).IsUnique();
    }
}

public sealed class SmvSimulationResultConfiguration : IEntityTypeConfiguration<SmvSimulationResult>
{
    public void Configure(EntityTypeBuilder<SmvSimulationResult> builder)
    {
        builder.ToTable("SmvSimulationResults");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Pin).HasMaxLength(100).IsRequired();
        builder.Property(x => x.RpuNumber).HasMaxLength(100).IsRequired();
        builder.Property(x => x.RpuType).HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.FailureReason).HasMaxLength(1000);
        builder.Property(x => x.CurrentMarketValue).HasPrecision(18, 2);
        builder.Property(x => x.CurrentAssessedValue).HasPrecision(18, 2);
        builder.Property(x => x.SimulatedMarketValue).HasPrecision(18, 2);
        builder.Property(x => x.SimulatedAssessedValue).HasPrecision(18, 2);
        builder.Property(x => x.SimulatedTaxableAssessedValue).HasPrecision(18, 2);

        builder.HasOne<SmvSimulationRun>().WithMany().HasForeignKey(x => x.SmvSimulationRunId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<RealPropertyUnit>().WithMany().HasForeignKey(x => x.RpuId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PropertyEntity>().WithMany().HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Assessment>().WithMany().HasForeignKey(x => x.CurrentAssessmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.CurrentClassification).WithMany().HasForeignKey(x => x.CurrentClassificationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.SimulatedClassification).WithMany().HasForeignKey(x => x.SimulatedClassificationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Prime.Domain.Entities.Reference.Municipality>().WithMany().HasForeignKey(x => x.MunicipalityId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Prime.Domain.Entities.Reference.Barangay>().WithMany().HasForeignKey(x => x.BarangayId).OnDelete(DeleteBehavior.Restrict);

        // A unit is simulated once per run.
        builder.HasIndex(x => new { x.SmvSimulationRunId, x.RpuId }).IsUnique();
        builder.HasIndex(x => new { x.SmvSimulationRunId, x.Pin });
        builder.HasIndex(x => new { x.SmvSimulationRunId, x.BarangayId });
    }
}

/// <summary>Valuation tests (docs/analysis/smv-preparation-general-revision.md §4.3).</summary>
public sealed class ValuationTestRunConfiguration : IEntityTypeConfiguration<ValuationTestRun>
{
    public void Configure(EntityTypeBuilder<ValuationTestRun> builder)
    {
        builder.ToTable("ValuationTestRuns", t => t.HasCheckConstraint("CK_ValuationTestRuns_SalesPeriod",
            "\"SalesFrom\" IS NULL OR \"SalesTo\" IS NULL OR \"SalesFrom\" <= \"SalesTo\""));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Description).HasMaxLength(1000);
        builder.HasOne(x => x.Smv).WithMany().HasForeignKey(x => x.SmvId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.Scope).WithOne().HasForeignKey(x => x.ValuationTestRunId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(x => x.Sales).WithOne().HasForeignKey(x => x.ValuationTestRunId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => x.SmvId);
    }
}

public sealed class ValuationTestScopeConfiguration : IEntityTypeConfiguration<ValuationTestScope>
{
    public void Configure(EntityTypeBuilder<ValuationTestScope> builder)
    {
        builder.ToTable("ValuationTestScopes");
        builder.HasKey(x => x.Id);
        builder.HasOne(x => x.Municipality).WithMany().HasForeignKey(x => x.MunicipalityId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.ValuationTestRunId, x.MunicipalityId }).IsUnique();
    }
}

public sealed class ValuationTestSaleConfiguration : IEntityTypeConfiguration<ValuationTestSale>
{
    public void Configure(EntityTypeBuilder<ValuationTestSale> builder)
    {
        builder.ToTable("ValuationTestSales", t => t.HasCheckConstraint("CK_ValuationTestSales_RatioOrReason",
            "(\"Ratio\" IS NULL) = (\"ExclusionReason\" IS NOT NULL)"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.LandArea).HasPrecision(18, 4);
        builder.Property(x => x.LandAreaUnit).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Price).HasPrecision(18, 2);
        builder.Property(x => x.RateUnit).HasMaxLength(50);
        builder.Property(x => x.UnitValue).HasPrecision(18, 2);
        builder.Property(x => x.Value).HasPrecision(18, 2);
        builder.Property(x => x.Ratio).HasPrecision(18, 4);
        builder.Property(x => x.ExclusionReason).HasMaxLength(500);
        builder.HasOne<Prime.Domain.Entities.MarketData.MarketTransaction>().WithMany().HasForeignKey(x => x.MarketTransactionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SmvSchedule>().WithMany().HasForeignKey(x => x.SmvScheduleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Prime.Domain.Entities.Reference.Municipality>().WithMany().HasForeignKey(x => x.MunicipalityId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Barangay).WithMany().HasForeignKey(x => x.BarangayId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Classification).WithMany().HasForeignKey(x => x.ClassificationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.SubClassification).WithMany().HasForeignKey(x => x.SubClassificationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Prime.Domain.Entities.Reference.ActualUse>().WithMany().HasForeignKey(x => x.ActualUseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.ValuationTestRunId, x.MarketTransactionId }).IsUnique();
    }
}

/// <summary>SMV preparation work files (docs/analysis/smv-preparation-general-revision.md §4.2).</summary>
public sealed class SmvPreparationConfiguration : IEntityTypeConfiguration<SmvPreparation>
{
    public void Configure(EntityTypeBuilder<SmvPreparation> builder)
    {
        builder.ToTable("SmvPreparations", t =>
            t.HasCheckConstraint("CK_SmvPreparations_Cancelled", "(\"Status\" = 'Cancelled') = (\"CancellationReason\" IS NOT NULL)"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Title).HasMaxLength(300).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.Notes).HasMaxLength(4000);
        builder.Property(x => x.CancellationReason).HasMaxLength(1000);
        builder.HasOne(x => x.ProposedSmv).WithMany().HasForeignKey(x => x.ProposedSmvId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.Consultations).WithOne().HasForeignKey(x => x.SmvPreparationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.Events).WithOne().HasForeignKey(x => x.SmvPreparationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.ProposedSmvId).IsUnique();
        // One open preparation per revision year.
        builder.HasIndex(x => x.RevisionYear).IsUnique().HasFilter("\"Status\" <> 'Cancelled'");
    }
}

public sealed class SmvConsultationConfiguration : IEntityTypeConfiguration<SmvConsultation>
{
    public void Configure(EntityTypeBuilder<SmvConsultation> builder)
    {
        builder.ToTable("SmvConsultations", t => t.HasCheckConstraint("CK_SmvConsultations_Attendance", "\"Attendance\" IS NULL OR \"Attendance\" >= 0"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Mode).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Venue).HasMaxLength(300);
        builder.Property(x => x.MinutesReference).HasMaxLength(200);
        builder.Property(x => x.Notes).HasMaxLength(2000);
    }
}

public sealed class SmvPreparationEventConfiguration : IEntityTypeConfiguration<SmvPreparationEvent>
{
    public void Configure(EntityTypeBuilder<SmvPreparationEvent> builder)
    {
        builder.ToTable("SmvPreparationEvents");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Kind).HasConversion<string>().HasMaxLength(40);
        builder.Property(x => x.Reference).HasMaxLength(200);
        builder.Property(x => x.Note).HasMaxLength(4000);
        builder.HasIndex(x => new { x.SmvPreparationId, x.OccurredOn });
    }
}
