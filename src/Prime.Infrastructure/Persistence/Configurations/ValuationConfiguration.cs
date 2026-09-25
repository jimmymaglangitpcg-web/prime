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
        builder.HasMany(x => x.Lines).WithOne().HasForeignKey(x => x.ValuationId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ValuationLineConfiguration : IEntityTypeConfiguration<ValuationLine>
{
    public void Configure(EntityTypeBuilder<ValuationLine> builder)
    {
        builder.ToTable("ValuationLines", t =>
        {
            t.HasCheckConstraint("CK_ValuationLines_Sequence", "\"Sequence\" >= 1");
            t.HasCheckConstraint("CK_ValuationLines_MarketValue", "\"MarketValue\" >= 0");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Source).HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.Description).HasMaxLength(500);
        builder.Property(x => x.Quantity).HasPrecision(18, 4);
        builder.Property(x => x.Unit).HasMaxLength(50);
        builder.Property(x => x.UnitValue).HasPrecision(18, 2);
        builder.Property(x => x.MarketValue).HasPrecision(18, 2);
        builder.Property(x => x.BreakdownJson).HasColumnType("jsonb");
        builder.HasOne(x => x.Classification).WithMany().HasForeignKey(x => x.ClassificationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.SubClassification).WithMany().HasForeignKey(x => x.SubClassificationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ActualUse).WithMany().HasForeignKey(x => x.ActualUseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.SmvSchedule).WithMany().HasForeignKey(x => x.SmvScheduleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.ValuationId, x.Sequence }).IsUnique();
    }
}
