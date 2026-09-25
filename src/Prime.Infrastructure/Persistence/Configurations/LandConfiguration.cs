using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Prime.Domain.Entities;

namespace Prime.Infrastructure.Persistence.Configurations;

public sealed class LandConfiguration : IEntityTypeConfiguration<Land>
{
    public void Configure(EntityTypeBuilder<Land> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Area).HasPrecision(14, 4);
        builder.Property(x => x.AreaUnit).HasMaxLength(10);
        builder.Property(x => x.LocationFactor).HasPrecision(9, 6);
        builder.Property(x => x.RoadFrontage).HasPrecision(14, 4);
        builder.Property(x => x.Zoning).HasMaxLength(50);
        builder.Property(x => x.MarketValue).HasPrecision(18, 2);
        builder.Property(x => x.AssessedValue).HasPrecision(18, 2);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);

        builder.HasOne(x => x.Rpu).WithMany().HasForeignKey(x => x.RpuId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Property).WithMany().HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Classification).WithMany().HasForeignKey(x => x.ClassificationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ActualUse).WithMany().HasForeignKey(x => x.ActualUseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.SubClassification).WithMany().HasForeignKey(x => x.SubClassificationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Zone).WithMany().HasForeignKey(x => x.ZoneId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.RoadType).WithMany().HasForeignKey(x => x.RoadTypeId).OnDelete(DeleteBehavior.Restrict);

        // Exactly one Land row per RPU (CLAUDE.md §22) — was an unenforced
        // assumption until Phase 6's assessment logic needed it to actually
        // hold; safe additive change, verified no duplicates exist yet.
        builder.HasIndex(x => x.RpuId).IsUnique();
        builder.HasIndex(x => x.PropertyId);
        builder.HasMany(x => x.Strips).WithOne().HasForeignKey(x => x.LandId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.Improvements).WithOne().HasForeignKey(x => x.LandId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.Adjustments).WithOne().HasForeignKey(x => x.LandId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class LandStripConfiguration : IEntityTypeConfiguration<LandStrip>
{
    public void Configure(EntityTypeBuilder<LandStrip> builder)
    {
        builder.ToTable("LandStrips", t =>
        {
            t.HasCheckConstraint("CK_LandStrips_Area", "\"Area\" >= 0");
            t.HasCheckConstraint("CK_LandStrips_Sequence", "\"Sequence\" >= 1");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Area).HasPrecision(14, 4);
        builder.HasOne(x => x.Classification).WithMany().HasForeignKey(x => x.ClassificationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.SubClassification).WithMany().HasForeignKey(x => x.SubClassificationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ActualUse).WithMany().HasForeignKey(x => x.ActualUseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Zone).WithMany().HasForeignKey(x => x.ZoneId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.LandId, x.Sequence }).IsUnique();
    }
}

public sealed class LandImprovementConfiguration : IEntityTypeConfiguration<LandImprovement>
{
    public void Configure(EntityTypeBuilder<LandImprovement> builder)
    {
        builder.ToTable("LandImprovements", t =>
        {
            t.HasCheckConstraint("CK_LandImprovements_Quantity", "\"Quantity\" > 0");
            t.HasCheckConstraint("CK_LandImprovements_Sequence", "\"Sequence\" >= 1");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Quantity).HasPrecision(14, 4);
        builder.Property(x => x.Description).HasMaxLength(500);
        builder.HasOne(x => x.ImprovementKind).WithMany().HasForeignKey(x => x.ImprovementKindId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Classification).WithMany().HasForeignKey(x => x.ClassificationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ActualUse).WithMany().HasForeignKey(x => x.ActualUseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.LandId, x.Sequence }).IsUnique();
    }
}

public sealed class LandAdjustmentConfiguration : IEntityTypeConfiguration<LandAdjustment>
{
    public void Configure(EntityTypeBuilder<LandAdjustment> builder)
    {
        builder.ToTable("LandAdjustments");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.FactorCode).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Remarks).HasMaxLength(500);
        builder.HasOne<LandStrip>().WithMany().HasForeignKey(x => x.LandStripId).OnDelete(DeleteBehavior.Restrict);
        // A factor applies once per scope (the whole land, or one strip).
        builder.HasIndex(x => new { x.LandId, x.LandStripId, x.FactorCode }).IsUnique().AreNullsDistinct(false);
    }
}

public sealed class AdjustmentFactorConfiguration : IEntityTypeConfiguration<AdjustmentFactor>
{
    public void Configure(EntityTypeBuilder<AdjustmentFactor> builder)
    {
        Forms.ConfigurationMapping.ConfigureCommon(builder, "AdjustmentFactors",
            t => t.HasCheckConstraint("CK_AdjustmentFactors_Percent", "\"Percent\" > -100 AND \"Percent\" <= 1000"));
        builder.Property(x => x.Code).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Percent).HasPrecision(9, 4);
        builder.Property(x => x.Description).HasMaxLength(1000);
        builder.HasOne(x => x.Smv).WithMany().HasForeignKey(x => x.SmvId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Classification).WithMany().HasForeignKey(x => x.ClassificationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.SmvId, x.Code }).IsUnique().HasFilter(Forms.ConfigurationMapping.OpenApprovedFilter)
            .HasDatabaseName("UX_AdjustmentFactors_OpenApproved");
    }
}
