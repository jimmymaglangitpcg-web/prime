using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Prime.Domain.Entities;

namespace Prime.Infrastructure.Persistence.Configurations;

public sealed class RealPropertyUnitConfiguration : IEntityTypeConfiguration<RealPropertyUnit>
{
    public void Configure(EntityTypeBuilder<RealPropertyUnit> builder)
    {
        builder.ToTable("RealPropertyUnit", t =>
        {
            t.HasCheckConstraint("CK_RealPropertyUnit_LandNotSelf", "\"LandRpuId\" IS NULL OR \"LandRpuId\" <> \"Id\"");
            t.HasCheckConstraint("CK_RealPropertyUnit_HostNotSelf", "\"HostRpuId\" IS NULL OR \"HostRpuId\" <> \"Id\"");
            t.HasCheckConstraint("CK_RealPropertyUnit_PinSuffix", "\"PinSuffix\" IS NULL OR \"PinSuffix\" > 0");
        });
        builder.HasKey(x => x.Id);

        builder.Property(x => x.RpuNumber).HasMaxLength(50).IsRequired();
        builder.HasIndex(x => x.RpuNumber).IsUnique();

        builder.Property(x => x.RpuType).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);

        builder.HasOne(x => x.Property).WithMany().HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.PreviousRpu).WithMany().HasForeignKey(x => x.PreviousRpuId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.LandRpu).WithMany().HasForeignKey(x => x.LandRpuId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.HostRpu).WithMany().HasForeignKey(x => x.HostRpuId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.LandRpuId);
        builder.HasIndex(x => x.HostRpuId);
        // A postscript is never reused within the property (MRPAAO p.42–43: retired numbers keep history unique).
        builder.HasIndex(x => new { x.PropertyId, x.PinSuffix }).IsUnique().HasFilter("\"PinSuffix\" IS NOT NULL")
            .HasDatabaseName("UX_RealPropertyUnit_Property_PinSuffix");

        // Effective-date pattern — docs/DATABASE.md §4.
        builder.HasIndex(x => new { x.PropertyId, x.EffectivityDate });
    }
}
