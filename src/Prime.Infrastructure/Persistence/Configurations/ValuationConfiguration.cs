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
        builder.HasOne<Prime.Domain.Entities.Transactions.TransactionType>().WithMany().HasForeignKey(x => x.TransactionTypeId).OnDelete(DeleteBehavior.Restrict);

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
        builder.HasOne<IndependentAppraisal>().WithMany().HasForeignKey(x => x.IndependentAppraisalId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.Quantity).HasPrecision(18, 4);
        builder.Property(x => x.Unit).HasMaxLength(50);
        builder.Property(x => x.UnitValue).HasPrecision(18, 2);
        builder.Property(x => x.MarketValue).HasPrecision(18, 2);
        builder.Property(x => x.BreakdownJson).HasColumnType("jsonb");
        builder.HasOne(x => x.Classification).WithMany().HasForeignKey(x => x.ClassificationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.SubClassification).WithMany().HasForeignKey(x => x.SubClassificationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ActualUse).WithMany().HasForeignKey(x => x.ActualUseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.SmvSchedule).WithMany().HasForeignKey(x => x.SmvScheduleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.PricedClassification).WithMany().HasForeignKey(x => x.PricedClassificationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.PricedSubClassification).WithMany().HasForeignKey(x => x.PricedSubClassificationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.ValuationId, x.Sequence }).IsUnique();
    }
}

/// <summary>Independent appraisals (valuation-foundation.md §4.7): one current appraisal per subject.</summary>
public sealed class IndependentAppraisalConfiguration : IEntityTypeConfiguration<IndependentAppraisal>
{
    public void Configure(EntityTypeBuilder<IndependentAppraisal> builder)
    {
        builder.ToTable("IndependentAppraisals", t => t.HasCheckConstraint("CK_IndependentAppraisals_Value", "\"Value\" >= 0"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Subject).HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.Approach).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Value).HasPrecision(18, 2);
        builder.Property(x => x.Basis).HasMaxLength(2000).IsRequired();
        builder.Property(x => x.Evidence).HasMaxLength(2000).IsRequired();
        builder.Property(x => x.EndReason).HasMaxLength(500);
        builder.HasOne(x => x.Rpu).WithMany().HasForeignKey(x => x.RpuId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.Inputs).WithOne().HasForeignKey(x => x.IndependentAppraisalId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.Subject, x.SubjectId }).IsUnique().HasFilter("\"IsCurrent\"").HasDatabaseName("UX_IndependentAppraisals_Current");
        builder.HasIndex(x => x.RpuId);
    }
}

public sealed class IndependentAppraisalInputConfiguration : IEntityTypeConfiguration<IndependentAppraisalInput>
{
    public void Configure(EntityTypeBuilder<IndependentAppraisalInput> builder)
    {
        builder.ToTable("IndependentAppraisalInputs");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Value).HasPrecision(18, 6);
        builder.Property(x => x.Unit).HasMaxLength(30);
        builder.HasIndex(x => new { x.IndependentAppraisalId, x.Sequence }).IsUnique();
    }
}

/// <summary>Back-tax runs and their periods (valuation-foundation.md §4.8): one valuation and one assessment per period.</summary>
public sealed class BackTaxRunConfiguration : IEntityTypeConfiguration<BackTaxRun>
{
    public void Configure(EntityTypeBuilder<BackTaxRun> builder)
    {
        builder.ToTable("BackTaxRuns", t => t.HasCheckConstraint("CK_BackTaxRuns_Years", "\"DeclaredFromYear\" <= \"InitialAssessmentYear\""));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Basis).HasMaxLength(1000).IsRequired();
        builder.HasOne(x => x.Rpu).WithMany().HasForeignKey(x => x.RpuId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Prime.Domain.Entities.Transactions.TransactionType>().WithMany().HasForeignKey(x => x.TransactionTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.Periods).WithOne().HasForeignKey(x => x.BackTaxRunId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.RpuId);
    }
}

public sealed class BackTaxPeriodConfiguration : IEntityTypeConfiguration<BackTaxPeriod>
{
    public void Configure(EntityTypeBuilder<BackTaxPeriod> builder)
    {
        builder.ToTable("BackTaxPeriods", t => t.HasCheckConstraint("CK_BackTaxPeriods_Dates", "\"EndDate\" IS NULL OR \"EndDate\" >= \"StartDate\""));
        builder.HasKey(x => x.Id);
        builder.HasOne(x => x.Valuation).WithMany().HasForeignKey(x => x.ValuationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Assessment).WithMany().HasForeignKey(x => x.AssessmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.BackTaxRunId, x.Sequence }).IsUnique();
        builder.HasIndex(x => x.AssessmentId).IsUnique();
    }
}
