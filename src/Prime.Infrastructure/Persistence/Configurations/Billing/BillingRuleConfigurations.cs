using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Prime.Domain.Entities.Billing;
using Prime.Domain.Entities.Reference;
using Prime.Infrastructure.Persistence.Configurations.Reference;

namespace Prime.Infrastructure.Persistence.Configurations.Billing;

public sealed class TaxTypeConfiguration : LookupEntityConfiguration<TaxType>;

/// <summary>Shared mapping and constraints for billing rule tables (docs/BILLING.md §3).</summary>
internal static class BillingRuleMapping
{
    // "One open approved rule per scope key" — the approval-time
    // supersession the service performs, guaranteed by the database too.
    public const string OpenApprovedFilter = "\"Status\" = 'Approved' AND \"EndDate\" IS NULL";

    public static void ConfigureCommon<T>(EntityTypeBuilder<T> builder, string table, Action<TableBuilder<T>>? checks = null) where T : BillingRule
    {
        builder.ToTable(table, t =>
        {
            // EndDate is the inclusive last day, so a one-day rule has EndDate == EffectiveDate.
            t.HasCheckConstraint($"CK_{table}_EndDate", "\"EndDate\" IS NULL OR \"EndDate\" >= \"EffectiveDate\"");
            t.HasCheckConstraint($"CK_{table}_Approval",
                "(\"Status\" IN ('Approved', 'Posted')) = (\"ApprovedAt\" IS NOT NULL)");
            checks?.Invoke(t);
        });
        builder.HasKey(x => x.Id);

        builder.Property(x => x.LegalBasis).HasMaxLength(500).IsRequired();
        builder.Property(x => x.OrdinanceNumber).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Remarks).HasMaxLength(1000);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);

        builder.HasIndex(x => new { x.Status, x.EffectiveDate });
    }

    public static string Percent(string column) => $"\"{column}\" >= 0 AND \"{column}\" <= 100";
}

public sealed class TaxRateConfiguration : IEntityTypeConfiguration<TaxRate>
{
    public void Configure(EntityTypeBuilder<TaxRate> builder)
    {
        BillingRuleMapping.ConfigureCommon(builder, "TaxRates", t =>
            t.HasCheckConstraint("CK_TaxRates_Rate", BillingRuleMapping.Percent("Rate")));
        builder.Property(x => x.Rate).HasPrecision(9, 6);
        builder.HasOne(x => x.TaxType).WithMany().HasForeignKey(x => x.TaxTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Classification).WithMany().HasForeignKey(x => x.ClassificationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.TaxTypeId, x.ClassificationId })
            .IsUnique().AreNullsDistinct(false).HasFilter(BillingRuleMapping.OpenApprovedFilter).HasDatabaseName("UX_TaxRates_OpenApproved");
    }
}

public sealed class PaymentScheduleConfiguration : IEntityTypeConfiguration<PaymentSchedule>
{
    public void Configure(EntityTypeBuilder<PaymentSchedule> builder)
    {
        BillingRuleMapping.ConfigureCommon(builder, "PaymentSchedules");
        builder.HasMany(x => x.Installments).WithOne().HasForeignKey(x => x.PaymentScheduleId).OnDelete(DeleteBehavior.Restrict);
        // Single scope: a constant expression index — at most one open approved schedule.
        builder.HasIndex(x => x.Status).IsUnique().HasFilter(BillingRuleMapping.OpenApprovedFilter).HasDatabaseName("UX_PaymentSchedules_OpenApproved");
    }
}

public sealed class PaymentScheduleInstallmentConfiguration : IEntityTypeConfiguration<PaymentScheduleInstallment>
{
    public void Configure(EntityTypeBuilder<PaymentScheduleInstallment> builder)
    {
        builder.ToTable("PaymentScheduleInstallments", t =>
        {
            t.HasCheckConstraint("CK_PaymentScheduleInstallments_Sequence", "\"Sequence\" >= 1");
            t.HasCheckConstraint("CK_PaymentScheduleInstallments_DueMonth", "\"DueMonth\" BETWEEN 1 AND 12");
            t.HasCheckConstraint("CK_PaymentScheduleInstallments_DueDay", "\"DueDay\" BETWEEN 1 AND 31");
            t.HasCheckConstraint("CK_PaymentScheduleInstallments_Share", "\"SharePercent\" > 0 AND \"SharePercent\" <= 100");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.SharePercent).HasPrecision(9, 6);
        builder.HasIndex(x => new { x.PaymentScheduleId, x.Sequence }).IsUnique();
    }
}

public sealed class DiscountRuleConfiguration : IEntityTypeConfiguration<DiscountRule>
{
    public void Configure(EntityTypeBuilder<DiscountRule> builder)
    {
        BillingRuleMapping.ConfigureCommon(builder, "DiscountRules", t =>
        {
            t.HasCheckConstraint("CK_DiscountRules_Rate", BillingRuleMapping.Percent("Rate"));
            // Cutoff fields belong to (and are required for) advance-payment discounts only.
            t.HasCheckConstraint("CK_DiscountRules_Cutoff",
                "(\"Kind\" = 'AdvancePayment') = (\"CutoffMonth\" IS NOT NULL AND \"CutoffDay\" IS NOT NULL AND \"CutoffYearOffset\" IS NOT NULL) " +
                "AND (\"Kind\" = 'AdvancePayment' OR (\"CutoffMonth\" IS NULL AND \"CutoffDay\" IS NULL AND \"CutoffYearOffset\" IS NULL))");
            t.HasCheckConstraint("CK_DiscountRules_CutoffRange",
                "\"CutoffMonth\" IS NULL OR (\"CutoffMonth\" BETWEEN 1 AND 12 AND \"CutoffDay\" BETWEEN 1 AND 31)");
        });
        builder.Property(x => x.Rate).HasPrecision(9, 6);
        builder.Property(x => x.Kind).HasConversion<string>().HasMaxLength(30);
        builder.HasOne(x => x.TaxType).WithMany().HasForeignKey(x => x.TaxTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.Kind, x.TaxTypeId })
            .IsUnique().AreNullsDistinct(false).HasFilter(BillingRuleMapping.OpenApprovedFilter).HasDatabaseName("UX_DiscountRules_OpenApproved");
    }
}

public sealed class InterestRuleConfiguration : IEntityTypeConfiguration<InterestRule>
{
    public void Configure(EntityTypeBuilder<InterestRule> builder)
    {
        BillingRuleMapping.ConfigureCommon(builder, "InterestRules", t =>
        {
            t.HasCheckConstraint("CK_InterestRules_Rate", BillingRuleMapping.Percent("RatePerMonth"));
            t.HasCheckConstraint("CK_InterestRules_MaxMonths", "\"MaxMonths\" IS NULL OR \"MaxMonths\" > 0");
        });
        builder.Property(x => x.RatePerMonth).HasPrecision(9, 6);
        builder.Property(x => x.MonthCounting).HasConversion<string>().HasMaxLength(40);
        builder.HasOne(x => x.TaxType).WithMany().HasForeignKey(x => x.TaxTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.TaxTypeId)
            .IsUnique().AreNullsDistinct(false).HasFilter(BillingRuleMapping.OpenApprovedFilter).HasDatabaseName("UX_InterestRules_OpenApproved");
    }
}

public sealed class PenaltyRuleConfiguration : IEntityTypeConfiguration<PenaltyRule>
{
    public void Configure(EntityTypeBuilder<PenaltyRule> builder)
    {
        BillingRuleMapping.ConfigureCommon(builder, "PenaltyRules", t =>
        {
            t.HasCheckConstraint("CK_PenaltyRules_RateXorFixed", "(\"Rate\" IS NULL) <> (\"FixedAmount\" IS NULL)");
            t.HasCheckConstraint("CK_PenaltyRules_Rate", "\"Rate\" IS NULL OR (" + BillingRuleMapping.Percent("Rate") + ")");
            t.HasCheckConstraint("CK_PenaltyRules_FixedAmount", "\"FixedAmount\" IS NULL OR \"FixedAmount\" >= 0");
            t.HasCheckConstraint("CK_PenaltyRules_AppliesAfterDays", "\"AppliesAfterDays\" >= 0");
        });
        builder.Property(x => x.Rate).HasPrecision(9, 6);
        builder.Property(x => x.FixedAmount).HasPrecision(18, 2);
        builder.HasOne(x => x.TaxType).WithMany().HasForeignKey(x => x.TaxTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.TaxTypeId)
            .IsUnique().AreNullsDistinct(false).HasFilter(BillingRuleMapping.OpenApprovedFilter).HasDatabaseName("UX_PenaltyRules_OpenApproved");
    }
}

public sealed class TaxIncreaseCapRuleConfiguration : IEntityTypeConfiguration<TaxIncreaseCapRule>
{
    public void Configure(EntityTypeBuilder<TaxIncreaseCapRule> builder)
    {
        BillingRuleMapping.ConfigureCommon(builder, "TaxIncreaseCapRules", t =>
        {
            t.HasCheckConstraint("CK_TaxIncreaseCapRules_MaxIncreasePercent", "\"MaxIncreasePercent\" >= 0");
            // A cap always covers a bounded window (docs/BILLING.md §3.7).
            t.HasCheckConstraint("CK_TaxIncreaseCapRules_EndDate", "\"EndDate\" IS NOT NULL");
            // RA 12001 §29 ¶3 / IRR §55: the statutory cap is measured against the pre-SMV tax.
            t.HasCheckConstraint("CK_TaxIncreaseCapRules_StatutoryBaseline",
                "\"Basis\" <> 'StatutoryFirstYear' OR \"Baseline\" = 'TaxBeforeSmv'");
        });
        builder.Property(x => x.MaxIncreasePercent).HasPrecision(9, 6);
        builder.Property(x => x.Basis).HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.Baseline).HasConversion<string>().HasMaxLength(30);
        builder.HasOne(x => x.Smv).WithMany().HasForeignKey(x => x.SmvId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.TaxType).WithMany().HasForeignKey(x => x.TaxTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.SmvId, x.TaxTypeId, x.Basis });
        // Approved windows of one scope never overlap: EX_TaxIncreaseCapRules_NoOverlap,
        // an EXCLUDE USING gist constraint added in raw SQL by the BillingRules migration.
    }
}
