using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Prime.Domain.Entities.Content;

namespace Prime.Infrastructure.Persistence.Configurations.Content;

public sealed class ContentImportConfiguration : IEntityTypeConfiguration<ContentImport>
{
    public void Configure(EntityTypeBuilder<ContentImport> builder)
    {
        builder.ToTable("ContentImports", t => t.HasCheckConstraint("CK_ContentImports_Counts", "\"CreatedCount\" >= 0 AND \"ChangedCount\" >= 0"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Pack).HasMaxLength(64).IsRequired();
        builder.Property(x => x.PackVersion).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(1000);
        builder.Property(x => x.ManifestSha256).HasMaxLength(64).IsFixedLength().IsRequired();
        builder.Property(x => x.Fingerprint).HasMaxLength(64).IsFixedLength().IsRequired();
        builder.Property(x => x.FilesJson).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.WarningsJson).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.HasMany(x => x.Items).WithOne().HasForeignKey(x => x.ContentImportId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.Pack, x.ImportedAt });
    }
}

public sealed class ContentImportItemConfiguration : IEntityTypeConfiguration<ContentImportItem>
{
    public void Configure(EntityTypeBuilder<ContentImportItem> builder)
    {
        builder.ToTable("ContentImportItems");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.EntityType).HasMaxLength(60).IsRequired();
        builder.Property(x => x.Key).HasMaxLength(200).IsRequired(); // a unit value's natural key is long (SMV, class, sub-class, place, date)
        builder.Property(x => x.Action).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.ChangesJson).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.Source).HasMaxLength(500).IsRequired();
        builder.Property(x => x.FilePath).HasMaxLength(260).IsRequired();
        builder.HasIndex(x => new { x.ContentImportId, x.Sequence }).IsUnique();
        builder.HasIndex(x => new { x.EntityType, x.EntityId });
    }
}
