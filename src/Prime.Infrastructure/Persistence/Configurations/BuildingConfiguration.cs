using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Prime.Domain.Entities;

namespace Prime.Infrastructure.Persistence.Configurations;

public sealed class BuildingConfiguration : IEntityTypeConfiguration<Building>
{
    public void Configure(EntityTypeBuilder<Building> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.BuildingPermitNumber).HasMaxLength(100);
        builder.Property(x => x.CondominiumCertificateNumber).HasMaxLength(100);
        builder.HasMany(x => x.Floors).WithOne().HasForeignKey(x => x.BuildingId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.Materials).WithOne().HasForeignKey(x => x.BuildingId).OnDelete(DeleteBehavior.Restrict);

        builder.Property(x => x.FloorArea).HasPrecision(14, 4);
        builder.Property(x => x.TotalFloorArea).HasPrecision(14, 4);
        builder.Property(x => x.CompletionPercentage).HasPrecision(5, 2);
        builder.Property(x => x.MarketValue).HasPrecision(18, 2);
        builder.Property(x => x.Depreciation).HasPrecision(9, 6);
        builder.Property(x => x.DepreciatedValue).HasPrecision(18, 2);
        builder.Property(x => x.AssessedValue).HasPrecision(18, 2);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);

        builder.HasOne(x => x.Rpu).WithMany().HasForeignKey(x => x.RpuId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Property).WithMany().HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.BuildingType).WithMany().HasForeignKey(x => x.BuildingTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.StructuralType).WithMany().HasForeignKey(x => x.StructuralTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ActualUse).WithMany().HasForeignKey(x => x.ActualUseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Condition).WithMany().HasForeignKey(x => x.ConditionId).OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.Components).WithOne(x => x.Building).HasForeignKey(x => x.BuildingId).OnDelete(DeleteBehavior.Cascade);

        // Exactly one Building row per RPU (CLAUDE.md §22) — see LandConfiguration.
        builder.HasIndex(x => x.RpuId).IsUnique();
        builder.HasIndex(x => x.PropertyId);
    }
}

public sealed class BuildingComponentConfiguration : IEntityTypeConfiguration<BuildingComponent>
{
    public void Configure(EntityTypeBuilder<BuildingComponent> builder)
    {
        builder.HasKey(x => x.Id);
        // An additional item carries a cost: it is added to the construction cost (MRPAAO Att. 2).
        builder.ToTable(t => t.HasCheckConstraint("CK_BuildingComponents_AdditionalItemCost", "NOT \"IsAdditionalItem\" OR \"Cost\" IS NOT NULL"));
        builder.HasOne<BuildingUsePortion>().WithMany().HasForeignKey(x => x.BuildingUsePortionId).OnDelete(DeleteBehavior.Restrict);

        builder.Property(x => x.Description).HasMaxLength(500);
        builder.Property(x => x.Quantity).HasPrecision(14, 4);
        builder.Property(x => x.UnitCost).HasPrecision(18, 2);
        builder.Property(x => x.Cost).HasPrecision(18, 2);

        builder.HasOne(x => x.ComponentType).WithMany().HasForeignKey(x => x.ComponentTypeId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.BuildingId);
    }
}

public sealed class BuildingUsePortionConfiguration : IEntityTypeConfiguration<BuildingUsePortion>
{
    public void Configure(EntityTypeBuilder<BuildingUsePortion> builder)
    {
        builder.ToTable("BuildingUsePortions", t =>
        {
            t.HasCheckConstraint("CK_BuildingUsePortions_FloorArea", "\"FloorArea\" > 0");
            t.HasCheckConstraint("CK_BuildingUsePortions_Sequence", "\"Sequence\" >= 1");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.FloorArea).HasPrecision(14, 4);
        builder.HasOne<Building>().WithMany(x => x.UsePortions).HasForeignKey(x => x.BuildingId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Classification).WithMany().HasForeignKey(x => x.ClassificationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ActualUse).WithMany().HasForeignKey(x => x.ActualUseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.BuildingId, x.Sequence }).IsUnique();
        builder.HasIndex(x => new { x.BuildingId, x.ClassificationId, x.ActualUseId }).IsUnique();
    }
}

public sealed class BuildingFloorConfiguration : IEntityTypeConfiguration<BuildingFloor>
{
    public void Configure(EntityTypeBuilder<BuildingFloor> builder)
    {
        builder.ToTable("BuildingFloors", t =>
        {
            t.HasCheckConstraint("CK_BuildingFloors_FloorNumber", "\"FloorNumber\" >= 1");
            t.HasCheckConstraint("CK_BuildingFloors_Area", "\"Area\" > 0");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Area).HasPrecision(14, 4);
        builder.HasIndex(x => new { x.BuildingId, x.FloorNumber }).IsUnique();
    }
}

public sealed class BuildingMaterialConfiguration : IEntityTypeConfiguration<BuildingMaterial>
{
    public void Configure(EntityTypeBuilder<BuildingMaterial> builder)
    {
        builder.ToTable("BuildingMaterials", t =>
        {
            // A catalogue material, or "Others (specify)" — exactly one.
            t.HasCheckConstraint("CK_BuildingMaterials_Material", "(\"StructuralMaterialId\" IS NULL) <> (\"OtherSpecify\" IS NULL)");
            t.HasCheckConstraint("CK_BuildingMaterials_FloorNumber", "\"FloorNumber\" IS NULL OR \"FloorNumber\" >= 1");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.OtherSpecify).HasMaxLength(200);
        builder.HasOne(x => x.StructuralPart).WithMany().HasForeignKey(x => x.StructuralPartId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.StructuralMaterial).WithMany().HasForeignKey(x => x.StructuralMaterialId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.BuildingId);
    }
}
