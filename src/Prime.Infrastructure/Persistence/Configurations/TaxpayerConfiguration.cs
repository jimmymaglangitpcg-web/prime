using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Prime.Domain.Entities;

namespace Prime.Infrastructure.Persistence.Configurations;

public sealed class TaxpayerConfiguration : IEntityTypeConfiguration<Taxpayer>
{
    public void Configure(EntityTypeBuilder<Taxpayer> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.TaxpayerType).HasConversion<string>().HasMaxLength(20).IsRequired();

        builder.Property(x => x.LastName).HasMaxLength(150);
        builder.Property(x => x.FirstName).HasMaxLength(150);
        builder.Property(x => x.MiddleName).HasMaxLength(150);
        builder.Property(x => x.Suffix).HasMaxLength(20);
        builder.Property(x => x.CorporateName).HasMaxLength(300);
        builder.Property(x => x.Tin).HasMaxLength(20);
        builder.Property(x => x.Address).HasMaxLength(500);
        builder.Property(x => x.ContactNumber).HasMaxLength(30);
        builder.Property(x => x.Email).HasMaxLength(320);

        // Search indexes (CLAUDE.md §56). TIN is intentionally NOT unique —
        // see docs/DATABASE.md §13 open question re: multi-LGU TIN reuse.
        builder.HasIndex(x => x.LastName);
        builder.HasIndex(x => x.CorporateName);
        builder.HasIndex(x => x.Tin);

        builder.HasOne(x => x.Barangay).WithMany().HasForeignKey(x => x.BarangayId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Municipality).WithMany().HasForeignKey(x => x.MunicipalityId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Province).WithMany().HasForeignKey(x => x.ProvinceId).OnDelete(DeleteBehavior.Restrict);

        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
    }
}
