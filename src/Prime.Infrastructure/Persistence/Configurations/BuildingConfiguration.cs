using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Prime.Domain.Entities;

namespace Prime.Infrastructure.Persistence.Configurations;

public sealed class BuildingConfiguration : IEntityTypeConfiguration<Building>
{
    public void Configure(EntityTypeBuilder<Building> builder)
    {
        builder.HasKey(x => x.Id);

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

        builder.HasIndex(x => x.RpuId);
        builder.HasIndex(x => x.PropertyId);
    }
}

public sealed class BuildingComponentConfiguration : IEntityTypeConfiguration<BuildingComponent>
{
    public void Configure(EntityTypeBuilder<BuildingComponent> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Description).HasMaxLength(500);
        builder.Property(x => x.Quantity).HasPrecision(14, 4);
        builder.Property(x => x.UnitCost).HasPrecision(18, 2);
        builder.Property(x => x.Cost).HasPrecision(18, 2);

        builder.HasOne(x => x.ComponentType).WithMany().HasForeignKey(x => x.ComponentTypeId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.BuildingId);
    }
}
