using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Prime.Domain.Common;
using Prime.Domain.Entities;

namespace Prime.Infrastructure.Persistence.Configurations;

public sealed class ParcelConfiguration : IEntityTypeConfiguration<Parcel>
{
    public void Configure(EntityTypeBuilder<Parcel> builder)
    {
        builder.HasKey(x => x.Id);

        // Typed column so PostGIS itself rejects wrong-SRID or non-areal
        // geometry (docs/DATABASE.md §9, docs/GIS.md §2). Single polygons
        // are normalized to MultiPolygon by ParcelService before saving.
        builder.Property(x => x.Geometry).HasColumnType($"geometry(MultiPolygon,{SpatialReference.StorageSrid})");
        builder.HasIndex(x => x.Geometry).HasMethod("GIST");

        builder.Property(x => x.Area).HasPrecision(14, 4);
        builder.Property(x => x.SurveyNumber).HasMaxLength(100);
        builder.Property(x => x.CadastralNumber).HasMaxLength(100);
        builder.Property(x => x.LotNumber).HasMaxLength(50);
        builder.Property(x => x.BlockNumber).HasMaxLength(50);

        builder.HasOne(x => x.Property).WithMany().HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Barangay).WithMany().HasForeignKey(x => x.BarangayId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Zone).WithMany().HasForeignKey(x => x.ZoneId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.PropertyId);
        builder.HasIndex(x => x.SurveyNumber);

        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);

        // Npgsql maps a uint row version to the xmin system column.
        builder.Property(x => x.RowVersion).IsRowVersion();

        // Its tax map section and parcel number there (docs/analysis/property-identification.md §3.3).
        builder.HasOne(x => x.Section).WithMany().HasForeignKey(x => x.SectionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.SectionId, x.ParcelNumber }).IsUnique().HasFilter("\"SectionId\" IS NOT NULL");
        builder.ToTable(t => t.HasCheckConstraint("CK_Parcels_SectionNumber", "(\"SectionId\" IS NULL) = (\"ParcelNumber\" IS NULL) AND (\"ParcelNumber\" IS NULL OR \"ParcelNumber\" > 0)"));
    }
}
