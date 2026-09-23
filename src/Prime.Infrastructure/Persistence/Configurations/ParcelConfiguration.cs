using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Prime.Domain.Entities;

namespace Prime.Infrastructure.Persistence.Configurations;

public sealed class ParcelConfiguration : IEntityTypeConfiguration<Parcel>
{
    public void Configure(EntityTypeBuilder<Parcel> builder)
    {
        builder.HasKey(x => x.Id);

        // SRID DOMAIN VERIFICATION REQUIRED — see Parcel.cs and
        // docs/DATABASE.md §9. 4326 (WGS84) pending confirmation.
        builder.Property(x => x.Geometry).HasColumnType("geometry");
        builder.HasIndex(x => x.Geometry).HasMethod("GIST");

        builder.Property(x => x.Area).HasPrecision(14, 4);
        builder.Property(x => x.SurveyNumber).HasMaxLength(100);
        builder.Property(x => x.LotNumber).HasMaxLength(50);
        builder.Property(x => x.BlockNumber).HasMaxLength(50);

        builder.HasOne(x => x.Property).WithMany().HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Barangay).WithMany().HasForeignKey(x => x.BarangayId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Zone).WithMany().HasForeignKey(x => x.ZoneId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.PropertyId);
        builder.HasIndex(x => x.SurveyNumber);

        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
    }
}
