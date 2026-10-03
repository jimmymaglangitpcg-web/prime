using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Prime.Domain.Entities;

namespace Prime.Infrastructure.Persistence.Configurations;

public sealed class AssessmentConfiguration : IEntityTypeConfiguration<Assessment>
{
    public void Configure(EntityTypeBuilder<Assessment> builder)
    {
        // One line: its level and percent are on the assessment too; several lines: on the lines only.
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_Assessments_Level", "(\"AssessmentLevelId\" IS NULL) = (\"AssessmentPercentage\" IS NULL)");
            // A posting stamp belongs to a posted assessment (older posted rows may lack one).
            t.HasCheckConstraint("CK_Assessments_Posted", "\"PostedAt\" IS NULL OR \"Status\" = 'Posted'");
        });
        builder.HasKey(x => x.Id);

        builder.Property(x => x.MarketValue).HasPrecision(18, 2);
        builder.Property(x => x.AssessmentPercentage).HasPrecision(9, 6);
        builder.Property(x => x.AssessedValue).HasPrecision(18, 2);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Remarks).HasMaxLength(2000);
        builder.Property(x => x.FaasNumber).HasMaxLength(100);
        // Year and quarter of the effectivity: derived, never typed (docs/analysis/valuation-foundation.md §4.2).
        builder.Property(x => x.EffectivityYear).HasComputedColumnSql("(EXTRACT(YEAR FROM \"EffectiveDate\"))::integer", stored: true);
        builder.Property(x => x.EffectivityQuarter).HasComputedColumnSql("(EXTRACT(QUARTER FROM \"EffectiveDate\"))::integer", stored: true);
        builder.Property(x => x.TransactionCode).HasMaxLength(20);
        builder.Property(x => x.EffectivityRule).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.EffectivityOverrideReason).HasMaxLength(1000);
        builder.HasOne<Prime.Domain.Entities.Transactions.TransactionType>().WithMany().HasForeignKey(x => x.TransactionTypeId).OnDelete(DeleteBehavior.Restrict);

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
        builder.HasMany(x => x.Lines).WithOne().HasForeignKey(x => x.AssessmentId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class AssessmentLineConfiguration : IEntityTypeConfiguration<AssessmentLine>
{
    public void Configure(EntityTypeBuilder<AssessmentLine> builder)
    {
        builder.ToTable("AssessmentLines", t =>
        {
            t.HasCheckConstraint("CK_AssessmentLines_Sequence", "\"Sequence\" >= 1");
            t.HasCheckConstraint("CK_AssessmentLines_Values", "\"MarketValue\" >= 0 AND \"AssessedValue\" >= 0");
            // A line is taxable or exempt; PartlyExempt summarises a TD only.
            t.HasCheckConstraint("CK_AssessmentLines_Taxability", "\"Taxability\" IN ('Taxable', 'Exempt')");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.MarketValue).HasPrecision(18, 2);
        builder.Property(x => x.AssessmentPercentage).HasPrecision(9, 6);
        builder.Property(x => x.AssessedValue).HasPrecision(18, 2);
        builder.HasOne(x => x.Classification).WithMany().HasForeignKey(x => x.ClassificationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ActualUse).WithMany().HasForeignKey(x => x.ActualUseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.PropertyType).WithMany().HasForeignKey(x => x.PropertyTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.AssessmentLevel).WithMany().HasForeignKey(x => x.AssessmentLevelId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.Taxability).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.TaxabilityNote).HasMaxLength(500);
        builder.HasOne(x => x.PropertyExemption).WithMany().HasForeignKey(x => x.PropertyExemptionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.PropertyExemptionId);
        // One row per (classification, actual use) — the grouping rule (§8.2).
        builder.HasIndex(x => new { x.AssessmentId, x.ClassificationId, x.ActualUseId }).IsUnique();
        builder.HasIndex(x => new { x.AssessmentId, x.Sequence }).IsUnique();
    }
}
