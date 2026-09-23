using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Prime.Domain.Entities;

namespace Prime.Infrastructure.Persistence.Configurations;

public sealed class RealPropertyUnitConfiguration : IEntityTypeConfiguration<RealPropertyUnit>
{
    public void Configure(EntityTypeBuilder<RealPropertyUnit> builder)
    {
        builder.ToTable("RealPropertyUnit");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.RpuNumber).HasMaxLength(50).IsRequired();
        builder.HasIndex(x => x.RpuNumber).IsUnique();

        builder.Property(x => x.RpuType).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);

        builder.HasOne(x => x.Property).WithMany().HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.PreviousRpu).WithMany().HasForeignKey(x => x.PreviousRpuId).OnDelete(DeleteBehavior.Restrict);

        // Effective-date pattern — docs/DATABASE.md §4.
        builder.HasIndex(x => new { x.PropertyId, x.EffectivityDate });
    }
}
