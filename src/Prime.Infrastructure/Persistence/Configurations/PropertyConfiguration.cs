using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Prime.Domain.Entities;

namespace Prime.Infrastructure.Persistence.Configurations;

public sealed class PropertyConfiguration : IEntityTypeConfiguration<PropertyEntity>
{
    public void Configure(EntityTypeBuilder<PropertyEntity> builder)
    {
        builder.ToTable("Property");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.BoundaryNorth).HasMaxLength(500);
        builder.Property(x => x.BoundaryEast).HasMaxLength(500);
        builder.Property(x => x.BoundarySouth).HasMaxLength(500);
        builder.Property(x => x.BoundaryWest).HasMaxLength(500);
        builder.HasOne(x => x.TitleType).WithMany().HasForeignKey(x => x.TitleTypeId).OnDelete(DeleteBehavior.Restrict);

        builder.Property(x => x.PropertyIdentificationNumber).HasMaxLength(50).IsRequired();
        builder.HasIndex(x => x.PropertyIdentificationNumber).IsUnique();

        builder.Property(x => x.Street).HasMaxLength(300);
        builder.Property(x => x.Sitio).HasMaxLength(200);
        builder.Property(x => x.LotNumber).HasMaxLength(50);
        builder.Property(x => x.BlockNumber).HasMaxLength(50);
        builder.Property(x => x.SurveyNumber).HasMaxLength(100);
        builder.Property(x => x.TitleNumber).HasMaxLength(100);
        builder.Property(x => x.TaxMapNumber).HasMaxLength(100);

        // Search indexes — CLAUDE.md §56, docs/DATABASE.md §11.
        builder.HasIndex(x => x.LotNumber);
        builder.HasIndex(x => x.TitleNumber);
        builder.HasIndex(x => x.SurveyNumber);
        builder.HasIndex(x => x.TaxMapNumber);

        builder.HasOne(x => x.Province).WithMany().HasForeignKey(x => x.ProvinceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Municipality).WithMany().HasForeignKey(x => x.MunicipalityId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Barangay).WithMany().HasForeignKey(x => x.BarangayId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Zone).WithMany().HasForeignKey(x => x.ZoneId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.BarangayId);
        builder.HasIndex(x => x.MunicipalityId);
        builder.HasIndex(x => x.ProvinceId);

        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
    }
}
