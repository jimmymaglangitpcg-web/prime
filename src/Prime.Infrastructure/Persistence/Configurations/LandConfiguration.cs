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
    }
}
