using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Prime.Domain.Entities.Reference;

namespace Prime.Infrastructure.Persistence.Configurations.Reference;

public sealed class ProvinceConfiguration : IEntityTypeConfiguration<Province>
{
    public void Configure(EntityTypeBuilder<Province> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.PsgcCode).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.HasIndex(x => x.PsgcCode).IsUnique();
        builder.Property(x => x.PinIndexNumber).HasMaxLength(3);
        builder.ToTable(t => t.HasCheckConstraint("CK_Province_PinIndexNumber", "\"PinIndexNumber\" ~ '^[0-9]{3}$'"));
        builder.HasIndex(x => x.PinIndexNumber).IsUnique().HasFilter("\"PinIndexNumber\" IS NOT NULL");
    }
}

public sealed class MunicipalityConfiguration : IEntityTypeConfiguration<Municipality>
{
    public void Configure(EntityTypeBuilder<Municipality> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.PsgcCode).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.HasIndex(x => x.PsgcCode).IsUnique();

        builder.HasOne(x => x.Province)
            .WithMany()
            .HasForeignKey(x => x.ProvinceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.ProvinceId);
        builder.Property(x => x.PinIndexNumber).HasMaxLength(3);
        builder.ToTable(t => t.HasCheckConstraint("CK_Municipality_PinIndexNumber", "\"PinIndexNumber\" ~ '^[0-9]{2,3}$'"));
        // A 2-digit number is unique within the province; 3-digit (city/MMA) numbers share the
        // provinces' number space, which the service checks (docs/analysis/property-identification.md §3.1).
        builder.HasIndex(x => new { x.ProvinceId, x.PinIndexNumber }).IsUnique().HasFilter("\"PinIndexNumber\" IS NOT NULL");
    }
}

public sealed class BarangayConfiguration : IEntityTypeConfiguration<Barangay>
{
    public void Configure(EntityTypeBuilder<Barangay> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.PsgcCode).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.HasIndex(x => x.PsgcCode).IsUnique();

        builder.HasOne(x => x.Municipality)
            .WithMany()
            .HasForeignKey(x => x.MunicipalityId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.MunicipalityId);

        builder.HasOne(x => x.CityDistrict).WithMany().HasForeignKey(x => x.CityDistrictId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Barangay>().WithMany().HasForeignKey(x => x.SplitFromBarangayId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.PinIndexNumber).HasMaxLength(4);
        builder.Property(x => x.RetirementReason).HasMaxLength(1000);
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_Barangay_PinIndexNumber", "\"PinIndexNumber\" ~ '^[0-9]{3,4}$'"); // width per Pin:BarangayIndexDigits
            t.HasCheckConstraint("CK_Barangay_Retired", "(\"RetiredOn\" IS NULL) = (\"RetirementReason\" IS NULL)");
        });
        // Never reused within the municipality or district — retired numbers included (§1 D.5).
        builder.HasIndex(x => new { x.MunicipalityId, x.CityDistrictId, x.PinIndexNumber }).IsUnique()
            .HasFilter("\"PinIndexNumber\" IS NOT NULL").AreNullsDistinct(false)
            .HasDatabaseName("UX_Barangay_PinIndexNumber");
    }
}

public sealed class CityDistrictConfiguration : IEntityTypeConfiguration<CityDistrict>
{
    public void Configure(EntityTypeBuilder<CityDistrict> builder)
    {
        builder.ToTable("CityDistricts", t => t.HasCheckConstraint("CK_CityDistricts_IndexNumber", "\"IndexNumber\" ~ '^[0-9]{2}$'"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.IndexNumber).HasMaxLength(2).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.HasOne(x => x.Municipality).WithMany().HasForeignKey(x => x.MunicipalityId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.MunicipalityId, x.IndexNumber }).IsUnique();
    }
}

public sealed class TaxMapSectionConfiguration : IEntityTypeConfiguration<TaxMapSection>
{
    public void Configure(EntityTypeBuilder<TaxMapSection> builder)
    {
        builder.ToTable("TaxMapSections", t =>
        {
            t.HasCheckConstraint("CK_TaxMapSections_IndexNumber", "\"IndexNumber\" ~ '^[0-9]{3}$'");
            t.HasCheckConstraint("CK_TaxMapSections_Retired", "(\"RetiredOn\" IS NULL) = (\"RetirementReason\" IS NULL)");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.IndexNumber).HasMaxLength(3).IsRequired();
        builder.Property(x => x.Remarks).HasMaxLength(1000);
        builder.Property(x => x.RetirementReason).HasMaxLength(1000);
        builder.HasOne(x => x.Barangay).WithMany().HasForeignKey(x => x.BarangayId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<TaxMapSection>().WithMany().HasForeignKey(x => x.SplitFromSectionId).OnDelete(DeleteBehavior.Restrict);
        // Never reused within the barangay, retired sections included.
        builder.HasIndex(x => new { x.BarangayId, x.IndexNumber }).IsUnique();
    }
}
