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
    }
}
