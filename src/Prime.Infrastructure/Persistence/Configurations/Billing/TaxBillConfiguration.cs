using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Prime.Domain.Entities;
using Prime.Domain.Entities.Billing;
using Prime.Domain.Entities.Reference;

namespace Prime.Infrastructure.Persistence.Configurations.Billing;

public sealed class TaxBillConfiguration : IEntityTypeConfiguration<TaxBill>
{
    public void Configure(EntityTypeBuilder<TaxBill> builder)
    {
        builder.ToTable("TaxBills", t =>
        {
            t.HasCheckConstraint("CK_TaxBills_AssessedValue", "\"AssessedValue\" >= 0");
            // A cancelled bill keeps PostedAt if it had been posted.
            t.HasCheckConstraint("CK_TaxBills_Posted",
                "(\"Status\" = 'Draft' AND \"PostedAt\" IS NULL) OR (\"Status\" = 'Posted' AND \"PostedAt\" IS NOT NULL) OR \"Status\" = 'Cancelled'");
            t.HasCheckConstraint("CK_TaxBills_Cancelled", "(\"Status\" = 'Cancelled') = (\"CancelledAt\" IS NOT NULL)");
        });
        builder.HasKey(x => x.Id);

        builder.Property(x => x.AssessedValue).HasPrecision(18, 2);
        builder.Property(x => x.Notes).HasMaxLength(4000);
        builder.Property(x => x.BillNumber).HasMaxLength(100);
        builder.HasIndex(x => x.BillNumber).IsUnique();
        builder.Property(x => x.CancellationReason).HasMaxLength(1000);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);

        builder.HasOne(x => x.Property).WithMany().HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Rpu).WithMany().HasForeignKey(x => x.RpuId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.TaxDeclaration).WithMany().HasForeignKey(x => x.TaxDeclarationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Assessment).WithMany().HasForeignKey(x => x.AssessmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Classification).WithMany().HasForeignKey(x => x.ClassificationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<TaxBill>().WithMany().HasForeignKey(x => x.SupersededByBillId).OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.TaxTypes).WithOne().HasForeignKey(x => x.TaxBillId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.Details).WithOne().HasForeignKey(x => x.TaxBillId).OnDelete(DeleteBehavior.Restrict);

        // Idempotency (docs/BILLING.md §4): one live bill per RPU, tax year and as-of date.
        builder.HasIndex(x => new { x.RpuId, x.TaxYear, x.AsOfDate }).IsUnique()
            .HasFilter("\"Status\" <> 'Cancelled'")
            .HasDatabaseName("UX_TaxBills_Rpu_TaxYear_AsOf_Live");
        // At most one posted bill per RPU and tax year — posting supersedes the previous one.
        builder.HasIndex(x => new { x.RpuId, x.TaxYear }).IsUnique()
            .HasFilter("\"Status\" = 'Posted'")
            .HasDatabaseName("UX_TaxBills_Rpu_TaxYear_Posted");
        builder.HasIndex(x => new { x.PropertyId, x.TaxYear });
    }
}

public sealed class TaxBillTaxTypeConfiguration : IEntityTypeConfiguration<TaxBillTaxType>
{
    public void Configure(EntityTypeBuilder<TaxBillTaxType> builder)
    {
        builder.ToTable("TaxBillTaxTypes", t =>
            t.HasCheckConstraint("CK_TaxBillTaxTypes_AnnualTax", "\"AnnualTax\" >= 0 AND \"AnnualTax\" <= \"ComputedAnnualTax\""));
        builder.HasKey(x => x.Id);

        builder.Property(x => x.RatePercent).HasPrecision(9, 6);
        builder.Property(x => x.ComputedAnnualTax).HasPrecision(18, 2);
        builder.Property(x => x.CapBaselineTax).HasPrecision(18, 2);
        builder.Property(x => x.CapLimit).HasPrecision(18, 2);
        builder.Property(x => x.AnnualTax).HasPrecision(18, 2);

        builder.HasOne(x => x.TaxType).WithMany().HasForeignKey(x => x.TaxTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<TaxRate>().WithMany().HasForeignKey(x => x.TaxRateId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<TaxIncreaseCapRule>().WithMany().HasForeignKey(x => x.CapRuleId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.TaxBillId, x.TaxTypeId }).IsUnique();
        builder.HasMany(x => x.Lines).WithOne().HasForeignKey(x => x.TaxBillTaxTypeId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class TaxBillTaxTypeLineConfiguration : IEntityTypeConfiguration<TaxBillTaxTypeLine>
{
    public void Configure(EntityTypeBuilder<TaxBillTaxTypeLine> builder)
    {
        builder.ToTable("TaxBillTaxTypeLines", t => t.HasCheckConstraint("CK_TaxBillTaxTypeLines_Values", "\"AssessedValue\" >= 0 AND \"Tax\" >= 0"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.AssessedValue).HasPrecision(18, 2);
        builder.Property(x => x.RatePercent).HasPrecision(9, 6);
        builder.Property(x => x.Tax).HasPrecision(18, 2);
        builder.HasOne<Classification>().WithMany().HasForeignKey(x => x.ClassificationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<TaxRate>().WithMany().HasForeignKey(x => x.TaxRateId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class TaxBillDetailConfiguration : IEntityTypeConfiguration<TaxBillDetail>
{
    public void Configure(EntityTypeBuilder<TaxBillDetail> builder)
    {
        builder.ToTable("TaxBillDetails", t =>
        {
            // Discounts are the only negative lines.
            t.HasCheckConstraint("CK_TaxBillDetails_Sign", "(\"Component\" = 'Discount') = (\"Amount\" < 0) OR \"Amount\" = 0");
            t.HasCheckConstraint("CK_TaxBillDetails_Installment", "\"InstallmentSequence\" >= 1");
        });
        builder.HasKey(x => x.Id);

        builder.Property(x => x.RatePercent).HasPrecision(9, 6);
        builder.Property(x => x.BaseAmount).HasPrecision(18, 2);
        builder.Property(x => x.Amount).HasPrecision(18, 2);
        builder.Property(x => x.Component).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Explanation).HasMaxLength(1000).IsRequired();

        builder.HasOne(x => x.TaxType).WithMany().HasForeignKey(x => x.TaxTypeId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.TaxBillId, x.LineNumber }).IsUnique();
    }
}
