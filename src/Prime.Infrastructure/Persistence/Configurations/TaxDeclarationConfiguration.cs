using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Prime.Domain.Entities;

namespace Prime.Infrastructure.Persistence.Configurations;

public sealed class TaxDeclarationConfiguration : IEntityTypeConfiguration<TaxDeclaration>
{
    public void Configure(EntityTypeBuilder<TaxDeclaration> builder)
    {
        builder.ToTable(t =>
            t.HasCheckConstraint("CK_TaxDeclarations_Cancelled", "(\"Status\" = 'Cancelled') = (\"CancelledAt\" IS NOT NULL)"));
        builder.HasKey(x => x.Id);

        builder.Property(x => x.TaxDeclarationNumber).HasMaxLength(50).IsRequired();
        builder.Property(x => x.CancellationReason).HasMaxLength(1000);
        builder.HasOne<TaxDeclaration>().WithMany().HasForeignKey(x => x.SupersededByTaxDeclarationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Prime.Domain.Entities.Transactions.PropertyTransaction>().WithMany().HasForeignKey(x => x.PropertyTransactionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.PropertyTransactionId);
        builder.HasOne(x => x.Assessment).WithMany().HasForeignKey(x => x.AssessmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.AssessmentId);
        builder.HasMany(x => x.Annotations).WithOne().HasForeignKey(x => x.TaxDeclarationId).OnDelete(DeleteBehavior.Restrict);
        // One current (Approved) Tax Declaration per RPU — docs/FORMS-REVISION-PLAN.md A4.
        builder.HasIndex(x => x.RpuId).IsUnique().HasFilter("\"Status\" = 'Approved'").HasDatabaseName("UX_TaxDeclarations_Rpu_Approved");
        builder.HasIndex(x => x.TaxDeclarationNumber).IsUnique();

        builder.Property(x => x.Taxability).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Remarks).HasMaxLength(2000);

        builder.HasOne(x => x.Rpu).WithMany().HasForeignKey(x => x.RpuId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Property).WithMany().HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Classification).WithMany().HasForeignKey(x => x.ClassificationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ActualUse).WithMany().HasForeignKey(x => x.ActualUseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.SubClassification).WithMany().HasForeignKey(x => x.SubClassificationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.PreviousTaxDeclaration).WithMany().HasForeignKey(x => x.PreviousTaxDeclarationId).OnDelete(DeleteBehavior.Restrict);

        // Effective-date / lookup patterns — docs/DATABASE.md §4/§11.
        builder.HasIndex(x => new { x.RpuId, x.EffectivityDate });
        builder.HasIndex(x => x.AssessmentYear);
        builder.HasIndex(x => x.PropertyId);
    }
}
