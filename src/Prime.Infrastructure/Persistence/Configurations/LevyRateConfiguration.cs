using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Prime.Domain.Entities;
using Prime.Infrastructure.Persistence.Configurations.Forms;

namespace Prime.Infrastructure.Persistence.Configurations;

/// <summary>Levy rates for the report collectibles (docs/analysis/reporting.md §10, Q18).</summary>
public sealed class LevyRateConfiguration : IEntityTypeConfiguration<LevyRate>
{
    public void Configure(EntityTypeBuilder<LevyRate> builder)
    {
        ConfigurationMapping.ConfigureCommon(builder, "LevyRates", t =>
            t.HasCheckConstraint("CK_LevyRates_Rate", "\"RatePercent\" > 0 AND \"RatePercent\" <= 100"));
        builder.Property(x => x.Code).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Kind).HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.RatePercent).HasPrecision(9, 6);
        builder.Property(x => x.Description).HasMaxLength(1000);
        builder.HasOne(x => x.Municipality).WithMany().HasForeignKey(x => x.MunicipalityId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Classification).WithMany().HasForeignKey(x => x.ClassificationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.Code).IsUnique().HasFilter(ConfigurationMapping.OpenApprovedFilter).HasDatabaseName("UX_LevyRates_OpenApproved");
        builder.HasIndex(x => new { x.Code, x.EffectiveDate });
    }
}
