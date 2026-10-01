using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Prime.Domain.Entities;
using Prime.Infrastructure.Persistence.Configurations.Forms;

namespace Prime.Infrastructure.Persistence.Configurations;

/// <summary>The SMV's building tables (docs/analysis/valuation-foundation.md §4.5): one open approved version per scope.</summary>
public sealed class SmvBuildingCostConfiguration : IEntityTypeConfiguration<SmvBuildingCost>
{
    public void Configure(EntityTypeBuilder<SmvBuildingCost> builder)
    {
        ConfigurationMapping.ConfigureCommon(builder, "SmvBuildingCosts",
            t => t.HasCheckConstraint("CK_SmvBuildingCosts_Cost", "\"CostPerSquareMetre\" > 0"));
        builder.Property(x => x.CostPerSquareMetre).HasPrecision(18, 2);
        builder.HasOne(x => x.Smv).WithMany().HasForeignKey(x => x.SmvId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.StructuralType).WithMany().HasForeignKey(x => x.StructuralTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.BuildingType).WithMany().HasForeignKey(x => x.BuildingTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Classification).WithMany().HasForeignKey(x => x.ClassificationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.SmvId, x.StructuralTypeId, x.BuildingTypeId, x.ClassificationId }).IsUnique().AreNullsDistinct(false)
            .HasFilter(ConfigurationMapping.OpenApprovedFilter).HasDatabaseName("UX_SmvBuildingCosts_OpenApproved");
    }
}

public sealed class SmvExtraItemCostConfiguration : IEntityTypeConfiguration<SmvExtraItemCost>
{
    public void Configure(EntityTypeBuilder<SmvExtraItemCost> builder)
    {
        ConfigurationMapping.ConfigureCommon(builder, "SmvExtraItemCosts",
            t => t.HasCheckConstraint("CK_SmvExtraItemCosts_Cost", "\"UnitCost\" > 0"));
        builder.Property(x => x.Unit).HasMaxLength(30).IsRequired();
        builder.Property(x => x.UnitCost).HasPrecision(18, 2);
        builder.HasOne(x => x.Smv).WithMany().HasForeignKey(x => x.SmvId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ComponentType).WithMany().HasForeignKey(x => x.ComponentTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.SmvId, x.ComponentTypeId }).IsUnique()
            .HasFilter(ConfigurationMapping.OpenApprovedFilter).HasDatabaseName("UX_SmvExtraItemCosts_OpenApproved");
    }
}

public sealed class SmvDepreciationScheduleConfiguration : IEntityTypeConfiguration<SmvDepreciationSchedule>
{
    public void Configure(EntityTypeBuilder<SmvDepreciationSchedule> builder)
    {
        ConfigurationMapping.ConfigureCommon(builder, "SmvDepreciationSchedules",
            t => t.HasCheckConstraint("CK_SmvDepreciationSchedules_Remaining", "\"MinimumRemainingPercent\" >= 0 AND \"MinimumRemainingPercent\" <= 100"));
        builder.Property(x => x.Reading).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.MinimumRemainingPercent).HasPrecision(9, 4);
        builder.HasMany(x => x.Rows).WithOne().HasForeignKey(x => x.SmvDepreciationScheduleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Smv).WithMany().HasForeignKey(x => x.SmvId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.StructuralType).WithMany().HasForeignKey(x => x.StructuralTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.SmvId, x.StructuralTypeId }).IsUnique()
            .HasFilter(ConfigurationMapping.OpenApprovedFilter).HasDatabaseName("UX_SmvDepreciationSchedules_OpenApproved");
    }
}

public sealed class SmvDepreciationRowConfiguration : IEntityTypeConfiguration<SmvDepreciationRow>
{
    public void Configure(EntityTypeBuilder<SmvDepreciationRow> builder)
    {
        builder.ToTable("SmvDepreciationRows", t =>
        {
            t.HasCheckConstraint("CK_SmvDepreciationRows_Percent", "\"Percent\" >= 0 AND \"Percent\" <= 100");
            t.HasCheckConstraint("CK_SmvDepreciationRows_Ages", "\"FromAge\" >= 0 AND (\"ToAge\" IS NULL OR \"ToAge\" >= \"FromAge\")");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Percent).HasPrecision(9, 4);
        builder.HasIndex(x => new { x.SmvDepreciationScheduleId, x.Sequence }).IsUnique();
    }
}
