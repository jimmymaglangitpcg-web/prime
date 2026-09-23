using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Prime.Domain.Entities;

namespace Prime.Infrastructure.Persistence.Configurations;

public sealed class SmvScheduleConfiguration : IEntityTypeConfiguration<SmvSchedule>
{
    public void Configure(EntityTypeBuilder<SmvSchedule> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Unit).HasMaxLength(20).IsRequired();
        builder.Property(x => x.MarketValue).HasPrecision(18, 2);
        builder.Property(x => x.MinimumValue).HasPrecision(18, 2);
        builder.Property(x => x.MaximumValue).HasPrecision(18, 2);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);

        builder.HasOne(x => x.Smv).WithMany().HasForeignKey(x => x.SmvId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Classification).WithMany().HasForeignKey(x => x.ClassificationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ActualUse).WithMany().HasForeignKey(x => x.ActualUseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.PropertyType).WithMany().HasForeignKey(x => x.PropertyTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Zone).WithMany().HasForeignKey(x => x.ZoneId).OnDelete(DeleteBehavior.Restrict);

        // Effective-date composite index — docs/DATABASE.md §4.
        builder.HasIndex(x => new { x.ClassificationId, x.ActualUseId, x.PropertyTypeId, x.ZoneId, x.EffectiveDate });
    }
}
