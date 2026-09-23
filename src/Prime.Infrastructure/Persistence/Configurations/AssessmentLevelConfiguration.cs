using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Prime.Domain.Entities;

namespace Prime.Infrastructure.Persistence.Configurations;

public sealed class AssessmentLevelConfiguration : IEntityTypeConfiguration<AssessmentLevel>
{
    public void Configure(EntityTypeBuilder<AssessmentLevel> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.OrdinanceNumber).HasMaxLength(50).IsRequired();
        builder.Property(x => x.LowerValue).HasPrecision(18, 2);
        builder.Property(x => x.UpperValue).HasPrecision(18, 2);
        builder.Property(x => x.AssessmentPercentage).HasPrecision(9, 6);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);

        builder.HasOne(x => x.Classification).WithMany().HasForeignKey(x => x.ClassificationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ActualUse).WithMany().HasForeignKey(x => x.ActualUseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.PropertyType).WithMany().HasForeignKey(x => x.PropertyTypeId).OnDelete(DeleteBehavior.Restrict);

        // Effective-date composite index — docs/DATABASE.md §4.
        builder.HasIndex(x => new { x.ClassificationId, x.ActualUseId, x.PropertyTypeId, x.EffectiveDate });
    }
}
