using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Prime.Domain.Entities.Documents;

namespace Prime.Infrastructure.Persistence.Configurations.Documents;

public sealed class DocumentConfiguration : IEntityTypeConfiguration<Document>
{
    public void Configure(EntityTypeBuilder<Document> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Bucket).HasMaxLength(100).IsRequired();
        builder.Property(x => x.StoragePath).HasMaxLength(1000).IsRequired();
        builder.Property(x => x.FileName).HasMaxLength(300).IsRequired();
        builder.Property(x => x.ContentType).HasMaxLength(150).IsRequired();
        builder.Property(x => x.RelatedEntityType).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);

        builder.HasOne(x => x.DocumentType).WithMany().HasForeignKey(x => x.DocumentTypeId).OnDelete(DeleteBehavior.Restrict);

        // The polymorphic (RelatedEntityType, RelatedEntityId) pair is not
        // a DB foreign key by construction (it can point at any entity
        // table) — indexed for "documents for this record" lookups.
        builder.HasIndex(x => new { x.RelatedEntityType, x.RelatedEntityId });
    }
}
