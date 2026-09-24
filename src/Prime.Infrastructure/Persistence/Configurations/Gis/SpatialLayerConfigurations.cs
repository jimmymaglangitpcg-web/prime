using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Prime.Domain.Common;
using Prime.Domain.Entities.Gis;

namespace Prime.Infrastructure.Persistence.Configurations.Gis;

/// <summary>Shared mapping for effective-dated reference-layer tables (docs/GIS.md §3).</summary>
internal static class SpatialLayerMapping
{
    public static void ConfigureCommon<T>(EntityTypeBuilder<T> builder, string table, string geometryType) where T : SpatialLayerFeature
    {
        builder.ToTable(table, t =>
            t.HasCheckConstraint($"CK_{table}_EndDate", "\"EndDate\" IS NULL OR \"EndDate\" > \"EffectiveDate\""));
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Source).HasMaxLength(300).IsRequired();
        builder.Property(x => x.SourceReference).HasMaxLength(300);
        builder.Property("Geometry").HasColumnType($"geometry({geometryType},{SpatialReference.StorageSrid})").IsRequired();

        builder.HasIndex("Geometry").HasMethod("GIST");
        builder.HasIndex(x => x.ImportBatchId);
    }
}

public sealed class BarangayBoundaryConfiguration : IEntityTypeConfiguration<BarangayBoundary>
{
    public void Configure(EntityTypeBuilder<BarangayBoundary> builder)
    {
        SpatialLayerMapping.ConfigureCommon(builder, "BarangayBoundaries", "MultiPolygon");
        builder.HasOne(x => x.Barangay).WithMany().HasForeignKey(x => x.BarangayId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.BarangayId, x.EffectiveDate });
        // At most one current version per barangay — enforced by the database, not just the import code.
        builder.HasIndex(x => x.BarangayId).IsUnique().HasFilter("\"EndDate\" IS NULL").HasDatabaseName("UX_BarangayBoundaries_Current");
    }
}

public sealed class ZoneBoundaryConfiguration : IEntityTypeConfiguration<ZoneBoundary>
{
    public void Configure(EntityTypeBuilder<ZoneBoundary> builder)
    {
        SpatialLayerMapping.ConfigureCommon(builder, "ZoneBoundaries", "MultiPolygon");
        builder.HasOne(x => x.Zone).WithMany().HasForeignKey(x => x.ZoneId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.ZoneId, x.EffectiveDate });
        builder.HasIndex(x => x.ZoneId).IsUnique().HasFilter("\"EndDate\" IS NULL").HasDatabaseName("UX_ZoneBoundaries_Current");
    }
}

public sealed class RoadSegmentConfiguration : IEntityTypeConfiguration<RoadSegment>
{
    public void Configure(EntityTypeBuilder<RoadSegment> builder)
    {
        SpatialLayerMapping.ConfigureCommon(builder, "RoadSegments", "MultiLineString");
        builder.Property(x => x.Code).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(200);
        builder.HasOne(x => x.RoadType).WithMany().HasForeignKey(x => x.RoadTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.Code, x.EffectiveDate });
        builder.HasIndex(x => x.Code).IsUnique().HasFilter("\"EndDate\" IS NULL").HasDatabaseName("UX_RoadSegments_Current");
    }
}
