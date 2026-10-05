using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Prime.Domain.Entities;

namespace Prime.Infrastructure.Persistence.Configurations;

/// <summary>Sub-class criteria of an SMV, SMV Form 1 (docs/analysis/smv-preparation-general-revision.md §4.2).</summary>
public sealed class SmvSubClassCriterionConfiguration : IEntityTypeConfiguration<SmvSubClassCriterion>
{
    public void Configure(EntityTypeBuilder<SmvSubClassCriterion> builder)
    {
        builder.ToTable("SmvSubClassCriteria", t => t.HasCheckConstraint("CK_SmvSubClassCriteria_Sequence", "\"Sequence\" > 0"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Criteria).HasMaxLength(4000).IsRequired();
        builder.HasOne<Smv>().WithMany().HasForeignKey(x => x.SmvId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Classification).WithMany().HasForeignKey(x => x.ClassificationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.SubClassification).WithMany().HasForeignKey(x => x.SubClassificationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.SmvId, x.ClassificationId, x.SubClassificationId }).IsUnique();
    }
}
