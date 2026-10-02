using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Prime.Domain.Entities;

namespace Prime.Infrastructure.Persistence.Configurations;

/// <summary>Territorial changes and barangay parts (docs/analysis/identification-numbering.md §4.3).</summary>
public sealed class TerritorialChangeJobConfiguration : IEntityTypeConfiguration<TerritorialChangeJob>
{
    public void Configure(EntityTypeBuilder<TerritorialChangeJob> builder)
    {
        builder.ToTable("TerritorialChangeJobs", t =>
            t.HasCheckConstraint("CK_TerritorialChangeJobs_Approval", "(\"Status\" = 'Approved') = (\"ApprovedAt\" IS NOT NULL)"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Kind).HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.PinMode).HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.RunStatus).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.LegalBasis).HasMaxLength(500).IsRequired();
        builder.Property(x => x.Remarks).HasMaxLength(1000);
        builder.HasMany(x => x.Mappings).WithOne().HasForeignKey(x => x.TerritorialChangeJobId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.Items).WithOne().HasForeignKey(x => x.TerritorialChangeJobId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class TerritorialChangeMappingConfiguration : IEntityTypeConfiguration<TerritorialChangeMapping>
{
    public void Configure(EntityTypeBuilder<TerritorialChangeMapping> builder)
    {
        builder.ToTable("TerritorialChangeMappings");
        builder.HasKey(x => x.Id);
        builder.HasOne(x => x.SourceBarangay).WithMany().HasForeignKey(x => x.SourceBarangayId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.TargetBarangay).WithMany().HasForeignKey(x => x.TargetBarangayId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.TerritorialChangeJobId, x.SourceBarangayId }).IsUnique();
    }
}

public sealed class TerritorialChangeItemConfiguration : IEntityTypeConfiguration<TerritorialChangeItem>
{
    public void Configure(EntityTypeBuilder<TerritorialChangeItem> builder)
    {
        builder.ToTable("TerritorialChangeItems");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.OldPin).HasMaxLength(100).IsRequired();
        builder.Property(x => x.NewPin).HasMaxLength(100);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Error).HasMaxLength(1000);
        builder.HasOne<PropertyEntity>().WithMany().HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.TerritorialChangeJobId, x.PropertyId }).IsUnique();
    }
}

public sealed class PropertyBarangayPartConfiguration : IEntityTypeConfiguration<PropertyBarangayPart>
{
    public void Configure(EntityTypeBuilder<PropertyBarangayPart> builder)
    {
        builder.ToTable("PropertyBarangayParts", t =>
        {
            t.HasCheckConstraint("CK_PropertyBarangayParts_Area", "\"Area\" > 0");
            t.HasCheckConstraint("CK_PropertyBarangayParts_Share", "\"AssessedValueShare\" >= 0 AND \"AssessedValueShare\" <= 100");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Area).HasPrecision(18, 4);
        builder.Property(x => x.AssessedValueShare).HasPrecision(9, 4);
        builder.HasOne(x => x.Property).WithMany().HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Barangay).WithMany().HasForeignKey(x => x.BarangayId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.PropertyId, x.BarangayId }).IsUnique();
    }
}
