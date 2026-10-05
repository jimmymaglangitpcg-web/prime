using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Prime.Domain.Entities;

namespace Prime.Infrastructure.Persistence.Configurations;

/// <summary>Simulated assessment rows, and revenue and tax impact studies (docs/analysis/smv-preparation-general-revision.md §4.3, §4.5).</summary>
public sealed class SmvSimulationResultLineConfiguration : IEntityTypeConfiguration<SmvSimulationResultLine>
{
    public void Configure(EntityTypeBuilder<SmvSimulationResultLine> builder)
    {
        builder.ToTable("SmvSimulationResultLines");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.MarketValue).HasPrecision(18, 2);
        builder.Property(x => x.AssessedValue).HasPrecision(18, 2);
        builder.HasOne<SmvSimulationResult>().WithMany(x => x.Lines).HasForeignKey(x => x.SmvSimulationResultId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Prime.Domain.Entities.Reference.Classification>().WithMany().HasForeignKey(x => x.ClassificationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Prime.Domain.Entities.Reference.ActualUse>().WithMany().HasForeignKey(x => x.ActualUseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.SmvSimulationResultId, x.Sequence });
    }
}

public sealed class RevenueImpactStudyConfiguration : IEntityTypeConfiguration<RevenueImpactStudy>
{
    public void Configure(EntityTypeBuilder<RevenueImpactStudy> builder)
    {
        builder.ToTable("RevenueImpactStudies", t => t.HasCheckConstraint("CK_RevenueImpactStudies_Collection",
            "(\"ActualCollection\" IS NULL) = (\"Discounts\" IS NULL) AND COALESCE(\"ActualCollection\", 0) >= 0 AND COALESCE(\"Discounts\", 0) >= 0"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Title).HasMaxLength(300).IsRequired();
        builder.Property(x => x.ActualCollection).HasPrecision(18, 2);
        builder.Property(x => x.Discounts).HasPrecision(18, 2);
        builder.Property(x => x.CollectionSource).HasMaxLength(500);
        builder.Property(x => x.Notes).HasMaxLength(4000);
        builder.HasOne(x => x.SmvSimulationRun).WithMany().HasForeignKey(x => x.SmvSimulationRunId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.Rates).WithOne().HasForeignKey(x => x.RevenueImpactStudyId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(x => x.Options).WithOne().HasForeignKey(x => x.RevenueImpactStudyId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class RevenueImpactRateConfiguration : IEntityTypeConfiguration<RevenueImpactRate>
{
    public void Configure(EntityTypeBuilder<RevenueImpactRate> builder)
    {
        builder.ToTable("RevenueImpactRates", t => t.HasCheckConstraint("CK_RevenueImpactRates_Rate", "\"RatePercent\" >= 0 AND \"RatePercent\" <= 100"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Label).HasMaxLength(100).IsRequired();
        builder.Property(x => x.RatePercent).HasPrecision(9, 4);
        builder.Property(x => x.Source).HasMaxLength(300).IsRequired();
    }
}

public sealed class RevenueImpactOptionConfiguration : IEntityTypeConfiguration<RevenueImpactOption>
{
    public void Configure(EntityTypeBuilder<RevenueImpactOption> builder)
    {
        builder.ToTable("RevenueImpactOptions", t => t.HasCheckConstraint("CK_RevenueImpactOptions_Rate", "\"RatePercent\" >= 0 AND \"RatePercent\" <= 100"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        builder.Property(x => x.RatePercent).HasPrecision(9, 4);
        builder.Property(x => x.Description).HasMaxLength(1000);
        builder.HasMany(x => x.Levels).WithOne().HasForeignKey(x => x.RevenueImpactOptionId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => new { x.RevenueImpactStudyId, x.Sequence }).IsUnique();
    }
}

public sealed class RevenueImpactOptionLevelConfiguration : IEntityTypeConfiguration<RevenueImpactOptionLevel>
{
    public void Configure(EntityTypeBuilder<RevenueImpactOptionLevel> builder)
    {
        builder.ToTable("RevenueImpactOptionLevels", t => t.HasCheckConstraint("CK_RevenueImpactOptionLevels_Values",
            "\"Percent\" >= 0 AND \"Percent\" <= 100 AND \"LowerValue\" >= 0 AND (\"UpperValue\" IS NULL OR \"UpperValue\" > \"LowerValue\")"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.LowerValue).HasPrecision(18, 2);
        builder.Property(x => x.UpperValue).HasPrecision(18, 2);
        builder.Property(x => x.Percent).HasPrecision(9, 4);
        builder.HasOne(x => x.Classification).WithMany().HasForeignKey(x => x.ClassificationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ActualUse).WithMany().HasForeignKey(x => x.ActualUseId).OnDelete(DeleteBehavior.Restrict);
    }
}
