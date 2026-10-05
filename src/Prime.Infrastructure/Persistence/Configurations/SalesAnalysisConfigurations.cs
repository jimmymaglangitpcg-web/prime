using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Prime.Domain.Entities;
using Prime.Domain.Entities.MarketData;

namespace Prime.Infrastructure.Persistence.Configurations;

/// <summary>Sales analyses of an SMV preparation (docs/analysis/smv-preparation-general-revision.md §4.2).</summary>
public sealed class SmvTimeAdjustmentFactorConfiguration : IEntityTypeConfiguration<SmvTimeAdjustmentFactor>
{
    public void Configure(EntityTypeBuilder<SmvTimeAdjustmentFactor> builder)
    {
        builder.ToTable("SmvTimeAdjustmentFactors", t => t.HasCheckConstraint("CK_SmvTimeAdjustmentFactors_Valid",
            "\"PeriodFrom\" <= \"PeriodTo\" AND \"Factor\" > 0"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Factor).HasPrecision(9, 6);
        builder.Property(x => x.Source).HasMaxLength(300).IsRequired();
        builder.HasOne<SmvPreparation>().WithMany().HasForeignKey(x => x.SmvPreparationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.SmvPreparationId, x.PeriodFrom });
    }
}

public sealed class SalesAnalysisConfiguration : IEntityTypeConfiguration<SalesAnalysis>
{
    public void Configure(EntityTypeBuilder<SalesAnalysis> builder)
    {
        builder.ToTable("SalesAnalyses", t => t.HasCheckConstraint("CK_SalesAnalyses_Parameters",
            "\"RoundingIncrement\" >= 0 AND (\"RangeWidthPercent\" IS NULL OR (\"RangeWidthPercent\" > 0 AND \"RangeWidthPercent\" <= 100))"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.AreaUnit).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.RoundingIncrement).HasPrecision(18, 2);
        builder.Property(x => x.RangeWidthPercent).HasPrecision(9, 4);
        builder.Property(x => x.Notes).HasMaxLength(2000);
        builder.HasOne<SmvPreparation>().WithMany().HasForeignKey(x => x.SmvPreparationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Classification).WithMany().HasForeignKey(x => x.ClassificationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ActualUse).WithMany().HasForeignKey(x => x.ActualUseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.Scope).WithOne().HasForeignKey(x => x.SalesAnalysisId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(x => x.Sales).WithOne().HasForeignKey(x => x.SalesAnalysisId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(x => x.Groups).WithOne().HasForeignKey(x => x.SalesAnalysisId).OnDelete(DeleteBehavior.Restrict);
        // One analysis per class and use in a preparation.
        builder.HasIndex(x => new { x.SmvPreparationId, x.ClassificationId, x.ActualUseId }).IsUnique().AreNullsDistinct(false);
    }
}

public sealed class SalesAnalysisScopeConfiguration : IEntityTypeConfiguration<SalesAnalysisScope>
{
    public void Configure(EntityTypeBuilder<SalesAnalysisScope> builder)
    {
        builder.ToTable("SalesAnalysisScopes");
        builder.HasKey(x => x.Id);
        builder.HasOne(x => x.Municipality).WithMany().HasForeignKey(x => x.MunicipalityId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.SalesAnalysisId, x.MunicipalityId }).IsUnique();
    }
}

public sealed class SalesAnalysisSaleConfiguration : IEntityTypeConfiguration<SalesAnalysisSale>
{
    public void Configure(EntityTypeBuilder<SalesAnalysisSale> builder)
    {
        builder.ToTable("SalesAnalysisSales", t =>
        {
            t.HasCheckConstraint("CK_SalesAnalysisSales_LeftOut", "NOT \"LeftOut\" OR \"ExclusionReason\" IS NOT NULL");
            t.HasCheckConstraint("CK_SalesAnalysisSales_ValueOrReason", "(\"RoundedUnitValue\" IS NULL) = (\"ExclusionReason\" IS NOT NULL)");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Location).HasMaxLength(300);
        builder.Property(x => x.TaxDeclarationNumber).HasMaxLength(100);
        builder.Property(x => x.Pin).HasMaxLength(100);
        builder.Property(x => x.Area).HasPrecision(18, 4);
        builder.Property(x => x.Price).HasPrecision(18, 2);
        builder.Property(x => x.UnitPrice).HasPrecision(18, 2);
        builder.Property(x => x.TimeFactor).HasPrecision(9, 6);
        builder.Property(x => x.OtherAdjustmentPercent).HasPrecision(9, 4);
        builder.Property(x => x.AdjustedUnitPrice).HasPrecision(18, 2);
        builder.Property(x => x.RoundedUnitValue).HasPrecision(18, 2);
        builder.Property(x => x.ExclusionReason).HasMaxLength(500);
        builder.Property(x => x.Note).HasMaxLength(500);
        builder.HasOne<MarketTransaction>().WithMany().HasForeignKey(x => x.MarketTransactionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Prime.Domain.Entities.Reference.Municipality>().WithMany().HasForeignKey(x => x.MunicipalityId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Barangay).WithMany().HasForeignKey(x => x.BarangayId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.SubClassification).WithMany().HasForeignKey(x => x.SubClassificationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.SalesAnalysisId, x.MarketTransactionId }).IsUnique();
    }
}

public sealed class SalesAnalysisGroupConfiguration : IEntityTypeConfiguration<SalesAnalysisGroup>
{
    public void Configure(EntityTypeBuilder<SalesAnalysisGroup> builder)
    {
        builder.ToTable("SalesAnalysisGroups", t =>
        {
            t.HasCheckConstraint("CK_SalesAnalysisGroups_Bounds", "\"FromValue\" >= 0 AND \"FromValue\" <= \"ToValue\"");
            t.HasCheckConstraint("CK_SalesAnalysisGroups_Adopted",
                "\"AdoptedAt\" IS NULL OR (\"SmvScheduleId\" IS NOT NULL AND \"SubClassificationId\" IS NOT NULL AND \"AdoptedValue\" > 0)");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.FromValue).HasPrecision(18, 2);
        builder.Property(x => x.ToValue).HasPrecision(18, 2);
        builder.Property(x => x.AdoptedValue).HasPrecision(18, 2);
        builder.Property(x => x.Basis).HasMaxLength(1000);
        builder.HasOne(x => x.SubClassification).WithMany().HasForeignKey(x => x.SubClassificationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SmvSchedule>().WithMany().HasForeignKey(x => x.SmvScheduleId).OnDelete(DeleteBehavior.Restrict);
    }
}
