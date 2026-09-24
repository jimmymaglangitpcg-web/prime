using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Prime.Domain.Entities;

namespace Prime.Infrastructure.Persistence.Configurations;

public sealed class MachineryConfiguration : IEntityTypeConfiguration<Machinery>
{
    public void Configure(EntityTypeBuilder<Machinery> builder)
    {
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_MachineryUnits_ReplacementCost", "\"ReplacementCost\" IS NULL OR \"ReplacementCost\" >= 0");
            // LGC §224(a): replacement cost is the basis only for machinery that is not brand-new.
            t.HasCheckConstraint("CK_MachineryUnits_BrandNewNoReplacementCost", "NOT \"IsBrandNew\" OR \"ReplacementCost\" IS NULL");
        });
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Description).HasMaxLength(500);
        builder.Property(x => x.Brand).HasMaxLength(150);
        builder.Property(x => x.Model).HasMaxLength(150);
        builder.Property(x => x.SerialNumber).HasMaxLength(100);
        builder.Property(x => x.Capacity).HasPrecision(14, 4);
        builder.Property(x => x.CapacityUnit).HasMaxLength(20);

        builder.Property(x => x.AcquisitionCost).HasPrecision(18, 2);
        builder.Property(x => x.InstallationCost).HasPrecision(18, 2);
        builder.Property(x => x.OtherCost).HasPrecision(18, 2);
        builder.Property(x => x.ReplacementCost).HasPrecision(18, 2);
        builder.Property(x => x.Depreciation).HasPrecision(9, 6);
        builder.Property(x => x.MarketValue).HasPrecision(18, 2);
        builder.Property(x => x.AssessedValue).HasPrecision(18, 2);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);

        builder.HasOne(x => x.Rpu).WithMany().HasForeignKey(x => x.RpuId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Property).WithMany().HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.MachineryType).WithMany().HasForeignKey(x => x.MachineryTypeId).OnDelete(DeleteBehavior.Restrict);

        // Exactly one Machinery row per RPU (CLAUDE.md §22) — see LandConfiguration.
        builder.HasIndex(x => x.RpuId).IsUnique();
        builder.HasIndex(x => x.PropertyId);
        builder.HasIndex(x => x.SerialNumber);
    }
}
