using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Prime.Domain.Entities;
using Prime.Infrastructure.Persistence.Configurations.Forms;

namespace Prime.Infrastructure.Persistence.Configurations;

/// <summary>Report row maps (docs/analysis/reporting.md §10, Q15).</summary>
public sealed class ReportRowMapConfiguration : IEntityTypeConfiguration<ReportRowMap>
{
    public void Configure(EntityTypeBuilder<ReportRowMap> builder)
    {
        ConfigurationMapping.ConfigureCommon(builder, "ReportRowMaps");
        builder.Property(x => x.Code).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Definition).HasColumnType("jsonb").IsRequired();
        builder.HasIndex(x => x.Code).IsUnique().HasFilter(ConfigurationMapping.OpenApprovedFilter).HasDatabaseName("UX_ReportRowMaps_OpenApproved");
        builder.HasIndex(x => new { x.Code, x.EffectiveDate });
    }
}

/// <summary>Dated system parameters (docs/analysis/reporting.md §10, Q16).</summary>
public sealed class SystemParameterConfiguration : IEntityTypeConfiguration<SystemParameter>
{
    public void Configure(EntityTypeBuilder<SystemParameter> builder)
    {
        ConfigurationMapping.ConfigureCommon(builder, "SystemParameters");
        builder.Property(x => x.Code).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Value).HasPrecision(18, 6);
        builder.Property(x => x.Description).HasMaxLength(1000);
        builder.HasIndex(x => x.Code).IsUnique().HasFilter(ConfigurationMapping.OpenApprovedFilter).HasDatabaseName("UX_SystemParameters_OpenApproved");
        builder.HasIndex(x => new { x.Code, x.EffectiveDate });
    }
}
