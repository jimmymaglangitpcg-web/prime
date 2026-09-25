using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Prime.Domain.Entities;

namespace Prime.Infrastructure.Persistence.Configurations;

public sealed class AssessmentConfiguration : IEntityTypeConfiguration<Assessment>
{
    public void Configure(EntityTypeBuilder<Assessment> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.MarketValue).HasPrecision(18, 2);
        builder.Property(x => x.AssessmentPercentage).HasPrecision(9, 6);
        builder.Property(x => x.AssessedValue).HasPrecision(18, 2);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Remarks).HasMaxLength(2000);
        builder.Property(x => x.FaasNumber).HasMaxLength(100);

        builder.HasOne(x => x.Rpu).WithMany().HasForeignKey(x => x.RpuId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Property).WithMany().HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Valuation).WithMany().HasForeignKey(x => x.ValuationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.AssessmentLevel).WithMany().HasForeignKey(x => x.AssessmentLevelId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.PreviousAssessment).WithMany().HasForeignKey(x => x.PreviousAssessmentId).OnDelete(DeleteBehavior.Restrict);
        // Checkpoint C: GeneralRevisionJob now exists — wire the FK that
        // RevisionReference was a bare scalar for until this point.
        builder.HasOne<GeneralRevisionJob>().WithMany().HasForeignKey(x => x.RevisionReference).OnDelete(DeleteBehavior.Restrict);

        // Effective-date / history indexes — docs/DATABASE.md §4/§11.
        builder.HasIndex(x => new { x.RpuId, x.EffectiveDate });
        builder.HasIndex(x => x.AssessmentYear);
        builder.HasIndex(x => x.RevisionReference);
        builder.HasIndex(x => x.FaasNumber).IsUnique();
    }
}
