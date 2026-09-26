using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Prime.Domain.Entities.Collection;
using Prime.Infrastructure.Persistence.Configurations.Forms;
using Prime.Infrastructure.Persistence.Configurations.Reference;

namespace Prime.Infrastructure.Persistence.Configurations.Collection;

/// <summary>docs/analysis/collection.md §3: money is numeric(18,2); numbers and the idempotency key are unique.</summary>
public sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("Payments", t =>
        {
            t.HasCheckConstraint("CK_Payments_Amounts",
                "\"AmountDue\" > 0 AND \"Change\" >= 0 AND \"AmountTendered\" = \"AmountDue\" + \"Change\"");
            t.HasCheckConstraint("CK_Payments_Cancelled", "(\"Status\" = 'Posted') = (\"CancelledAt\" IS NULL)");
        });
        builder.HasKey(x => x.Id);

        builder.Property(x => x.TransactionNumber).HasMaxLength(100).IsRequired();
        builder.HasIndex(x => x.TransactionNumber).IsUnique();
        builder.Property(x => x.OfficialReceiptNumber).HasMaxLength(100).IsRequired();
        builder.HasIndex(x => x.OfficialReceiptNumber).IsUnique();
        builder.Property(x => x.IdempotencyKey).HasMaxLength(100).IsRequired();
        builder.HasIndex(x => x.IdempotencyKey).IsUnique();

        builder.Property(x => x.PayorName).HasMaxLength(300).IsRequired();
        builder.Property(x => x.PayorAddress).HasMaxLength(500);
        builder.Property(x => x.Office).HasMaxLength(200);
        builder.Property(x => x.LocationCode).HasMaxLength(50);
        builder.Property(x => x.Remarks).HasMaxLength(1000);
        builder.Property(x => x.AmountDue).HasPrecision(18, 2);
        builder.Property(x => x.AmountTendered).HasPrecision(18, 2);
        builder.Property(x => x.Change).HasPrecision(18, 2);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);

        builder.HasOne(x => x.PayorTaxpayer).WithMany().HasForeignKey(x => x.PayorTaxpayerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.Tenders).WithOne().HasForeignKey(x => x.PaymentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.Allocations).WithOne().HasForeignKey(x => x.PaymentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.Cancellations).WithOne(x => x.Payment).HasForeignKey(x => x.PaymentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Payment>().WithMany().HasForeignKey(x => x.ReplacesPaymentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.ReplacesPaymentId).IsUnique().HasFilter("\"ReplacesPaymentId\" IS NOT NULL");

        builder.HasIndex(x => new { x.PaymentDate, x.CashierUserId });
        builder.HasIndex(x => x.PayorTaxpayerId);
    }
}

public sealed class PaymentTenderConfiguration : IEntityTypeConfiguration<PaymentTender>
{
    public void Configure(EntityTypeBuilder<PaymentTender> builder)
    {
        builder.ToTable("PaymentTenders", t => t.HasCheckConstraint("CK_PaymentTenders_Amount", "\"Amount\" > 0"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Amount).HasPrecision(18, 2);
        builder.Property(x => x.Reference).HasMaxLength(200);
        builder.Property(x => x.Bank).HasMaxLength(200);
        builder.HasOne(x => x.PaymentMode).WithMany().HasForeignKey(x => x.PaymentModeId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class PaymentAllocationConfiguration : IEntityTypeConfiguration<PaymentAllocation>
{
    public void Configure(EntityTypeBuilder<PaymentAllocation> builder)
    {
        builder.ToTable("PaymentAllocations", t =>
        {
            // Discounts are the only negative lines; principal is never zero.
            t.HasCheckConstraint("CK_PaymentAllocations_Sign",
                "(\"Component\" = 'Discount' AND \"Amount\" < 0) OR (\"Component\" = 'Tax' AND \"Amount\" > 0) " +
                "OR (\"Component\" IN ('Penalty', 'Interest') AND \"Amount\" >= 0)");
        });
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Component).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.YearCategory).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.RatePercent).HasPrecision(9, 6);
        builder.Property(x => x.BaseAmount).HasPrecision(18, 2);
        builder.Property(x => x.Amount).HasPrecision(18, 2);
        builder.Property(x => x.Explanation).HasMaxLength(1000).IsRequired();
        builder.Property(x => x.AccountCode).HasMaxLength(50).IsRequired();
        builder.Property(x => x.AccountName).HasMaxLength(300).IsRequired();
        builder.Property(x => x.Fund).HasMaxLength(100);

        builder.HasOne(x => x.TaxBill).WithMany().HasForeignKey(x => x.TaxBillId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.TaxType).WithMany().HasForeignKey(x => x.TaxTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Prime.Domain.Entities.PropertyEntity>().WithMany().HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Prime.Domain.Entities.RealPropertyUnit>().WithMany().HasForeignKey(x => x.RpuId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<RevenueAccountMapping>().WithMany().HasForeignKey(x => x.RevenueAccountMappingId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.PaymentId, x.LineNumber }).IsUnique();
        // What is paid per installment and tax type (docs/analysis/collection.md §2).
        builder.HasIndex(x => new { x.RpuId, x.TaxYear, x.InstallmentSequence, x.TaxTypeId });
        builder.HasIndex(x => x.PropertyId);
    }
}

public sealed class PaymentCancellationConfiguration : IEntityTypeConfiguration<PaymentCancellation>
{
    public void Configure(EntityTypeBuilder<PaymentCancellation> builder)
    {
        builder.ToTable("PaymentCancellations", t =>
        {
            t.HasCheckConstraint("CK_PaymentCancellations_Decision",
                "(\"Status\" = 'Pending') = (\"DecidedAt\" IS NULL) AND (\"Status\" = 'Approved') = (\"Kind\" IS NOT NULL AND \"TransactionNumber\" IS NOT NULL)");
            t.HasCheckConstraint("CK_PaymentCancellations_Correction", "\"IsCorrection\" = (\"ReplacementRequestJson\" IS NOT NULL)");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Reason).HasMaxLength(1000).IsRequired();
        builder.Property(x => x.ReplacementRequestJson).HasColumnType("jsonb");
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Kind).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.DecisionRemarks).HasMaxLength(1000);
        builder.Property(x => x.TransactionNumber).HasMaxLength(100);
        builder.HasIndex(x => x.TransactionNumber).IsUnique().HasFilter("\"TransactionNumber\" IS NOT NULL");
        builder.HasOne<Payment>().WithMany().HasForeignKey(x => x.ReplacementPaymentId).OnDelete(DeleteBehavior.Restrict);
        // One open request per payment.
        builder.HasIndex(x => x.PaymentId).IsUnique().HasFilter("\"Status\" = 'Pending'").HasDatabaseName("UX_PaymentCancellations_Pending");
        builder.HasIndex(x => x.Status);
    }
}

public sealed class PaymentModeConfiguration : LookupEntityConfiguration<PaymentMode>;

public sealed class RevenueAccountMappingConfiguration : IEntityTypeConfiguration<RevenueAccountMapping>
{
    public void Configure(EntityTypeBuilder<RevenueAccountMapping> builder)
    {
        ConfigurationMapping.ConfigureCommon(builder, "RevenueAccountMappings");
        builder.Property(x => x.Component).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.YearCategory).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.AccountCode).HasMaxLength(50).IsRequired();
        builder.Property(x => x.AccountName).HasMaxLength(300).IsRequired();
        builder.Property(x => x.Fund).HasMaxLength(100);
        builder.HasOne(x => x.TaxType).WithMany().HasForeignKey(x => x.TaxTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.TaxTypeId, x.Component, x.YearCategory }).IsUnique()
            .HasFilter(ConfigurationMapping.OpenApprovedFilter)
            .HasDatabaseName("UX_RevenueAccountMappings_OpenApproved");
    }
}
