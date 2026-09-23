using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Prime.Domain.Entities;

namespace Prime.Infrastructure.Persistence.Configurations;

public sealed class ValuationConfiguration : IEntityTypeConfiguration<Valuation>
{
    public void Configure(EntityTypeBuilder<Valuation> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.ComputedMarketValue).HasPrecision(18, 2);
        builder.Property(x => x.SourceType).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.ValuationMethod).HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.BreakdownJson).HasColumnType("jsonb");

        builder.HasOne(x => x.Rpu).WithMany().HasForeignKey(x => x.RpuId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Property).WithMany().HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Smv).WithMany().HasForeignKey(x => x.SmvId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.SmvSchedule).WithMany().HasForeignKey(x => x.SmvScheduleId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.RpuId);
        builder.HasIndex(x => new { x.SourceType, x.SourceId, x.ComputedAt });
    }
}
