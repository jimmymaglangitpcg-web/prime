using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Prime.Domain.Entities;
using Prime.Domain.Entities.Reference;
using Prime.Infrastructure.Persistence.Configurations.Reference;

namespace Prime.Infrastructure.Persistence.Configurations;

public sealed class AnnotationTypeConfiguration : LookupEntityConfiguration<AnnotationType>;

public sealed class TaxDeclarationAnnotationConfiguration : IEntityTypeConfiguration<TaxDeclarationAnnotation>
{
    public void Configure(EntityTypeBuilder<TaxDeclarationAnnotation> builder)
    {
        builder.ToTable("TaxDeclarationAnnotations", t =>
            t.HasCheckConstraint("CK_TaxDeclarationAnnotations_Lifted", "(\"LiftedAt\" IS NULL) = (\"LiftReason\" IS NULL)"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Text).HasMaxLength(2000).IsRequired();
        builder.Property(x => x.ReferenceNumber).HasMaxLength(100);
        builder.Property(x => x.LiftReason).HasMaxLength(1000);
        builder.Property(x => x.LiftReference).HasMaxLength(100);
        builder.HasOne(x => x.AnnotationType).WithMany().HasForeignKey(x => x.AnnotationTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.TaxDeclarationId, x.EffectiveDate });
    }
}
