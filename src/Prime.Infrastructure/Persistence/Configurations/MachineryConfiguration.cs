using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Prime.Domain.Entities;

namespace Prime.Infrastructure.Persistence.Configurations;

public sealed class MachineryConfiguration : IEntityTypeConfiguration<Machinery>
{
    public void Configure(EntityTypeBuilder<Machinery> builder)
    {
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_MachineryUnits_ReplacementCost", "\"ReplacementCost\" IS NULL OR \"ReplacementCost\" >= 0");
            // LGC §224(a): replacement cost is the basis only for machinery that is not brand-new.
            t.HasCheckConstraint("CK_MachineryUnits_BrandNewNoReplacementCost", "NOT \"IsBrandNew\" OR \"ReplacementCost\" IS NULL");
        });
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Description).HasMaxLength(500);
        builder.Property(x => x.Brand).HasMaxLength(150);
        builder.Property(x => x.Model).HasMaxLength(150);
        builder.Property(x => x.SerialNumber).HasMaxLength(100);
        builder.Property(x => x.Capacity).HasPrecision(14, 4);
        builder.Property(x => x.CapacityUnit).HasMaxLength(20);

        builder.Property(x => x.AcquisitionCost).HasPrecision(18, 2);
        builder.Property(x => x.InstallationCost).HasPrecision(18, 2);
        builder.Property(x => x.OtherCost).HasPrecision(18, 2);
        builder.Property(x => x.ReplacementCost).HasPrecision(18, 2);
        builder.Property(x => x.Depreciation).HasPrecision(9, 6);
        builder.Property(x => x.MarketValue).HasPrecision(18, 2);
        builder.Property(x => x.AssessedValue).HasPrecision(18, 2);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.AcquisitionCurrency).HasMaxLength(3);
        builder.Property(x => x.ForeignAcquisitionCost).HasPrecision(18, 2);
        builder.Property(x => x.OriginCountry).HasMaxLength(100);
        builder.Property(x => x.PriceIndexSeries).HasMaxLength(30);
        builder.Property(x => x.IsInOperation).HasDefaultValue(true);
        builder.HasMany(x => x.CostItems).WithOne().HasForeignKey(x => x.MachineryId).OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Rpu).WithMany().HasForeignKey(x => x.RpuId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Property).WithMany().HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.MachineryType).WithMany().HasForeignKey(x => x.MachineryTypeId).OnDelete(DeleteBehavior.Restrict);

        // Exactly one Machinery row per RPU (CLAUDE.md §22) — see LandConfiguration.
        // Several machines per machinery RPU, one FAAS row each (MRPAAO Att. 3; docs/analysis/mrpaao-forms-model.md §8.3).
        builder.HasIndex(x => x.RpuId);
        builder.Property(x => x.ConversionFactor).HasPrecision(12, 6);
        builder.HasOne(x => x.Classification).WithMany().HasForeignKey(x => x.ClassificationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ActualUse).WithMany().HasForeignKey(x => x.ActualUseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.PropertyId);
        builder.HasIndex(x => x.SerialNumber);
    }
}

/// <summary>A machine's acquisition cost items (valuation-foundation.md §4.6).</summary>
public sealed class MachineryCostItemConfiguration : IEntityTypeConfiguration<MachineryCostItem>
{
    public void Configure(EntityTypeBuilder<MachineryCostItem> builder)
    {
        builder.ToTable("MachineryCostItems", t => t.HasCheckConstraint("CK_MachineryCostItems_Amount", "\"Amount\" >= 0"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Kind).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Amount).HasPrecision(18, 2);
        builder.Property(x => x.Description).HasMaxLength(200);
        builder.HasIndex(x => new { x.MachineryId, x.Sequence }).IsUnique();
    }
}

/// <summary>Exchange rates and price indices for machinery: one approved row per currency and date, per series and year.</summary>
public sealed class ExchangeRateConfiguration : IEntityTypeConfiguration<ExchangeRate>
{
    public void Configure(EntityTypeBuilder<ExchangeRate> builder)
    {
        builder.ToTable("ExchangeRates", t =>
        {
            t.HasCheckConstraint("CK_ExchangeRates_Rate", "\"PesosPerUnit\" > 0");
            t.HasCheckConstraint("CK_ExchangeRates_Approval", "(\"Status\" = 'Approved') = (\"ApprovedAt\" IS NOT NULL)");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        builder.Property(x => x.PesosPerUnit).HasPrecision(18, 6);
        builder.Property(x => x.Source).HasMaxLength(300).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Remarks).HasMaxLength(1000);
        builder.HasIndex(x => new { x.Currency, x.RateDate }).IsUnique().HasFilter("\"Status\" = 'Approved'").HasDatabaseName("UX_ExchangeRates_Approved");
    }
}

public sealed class PriceIndexConfiguration : IEntityTypeConfiguration<PriceIndex>
{
    public void Configure(EntityTypeBuilder<PriceIndex> builder)
    {
        builder.ToTable("PriceIndices", t =>
        {
            t.HasCheckConstraint("CK_PriceIndices_Value", "\"Value\" > 0");
            t.HasCheckConstraint("CK_PriceIndices_Approval", "(\"Status\" = 'Approved') = (\"ApprovedAt\" IS NOT NULL)");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Series).HasMaxLength(30).IsRequired();
        builder.Property(x => x.Value).HasPrecision(18, 6);
        builder.Property(x => x.Source).HasMaxLength(300).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Remarks).HasMaxLength(1000);
        builder.HasIndex(x => new { x.Series, x.Year }).IsUnique().HasFilter("\"Status\" = 'Approved'").HasDatabaseName("UX_PriceIndices_Approved");
    }
}
