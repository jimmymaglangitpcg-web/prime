using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Prime.Domain.Entities;
using Prime.Domain.Entities.Notices;

namespace Prime.Infrastructure.Persistence.Configurations.Notices;

public sealed class NoticeOfAssessmentConfiguration : IEntityTypeConfiguration<NoticeOfAssessment>
{
    public void Configure(EntityTypeBuilder<NoticeOfAssessment> builder)
    {
        builder.ToTable("NoticesOfAssessment", t =>
        {
            t.HasCheckConstraint("CK_NoticesOfAssessment_Issued", "(\"Status\" IN ('Issued', 'Served')) <= (\"IssuedAt\" IS NOT NULL)");
            // A served notice has everything the appeal period depends on, and its proof.
            t.HasCheckConstraint("CK_NoticesOfAssessment_Served",
                "(\"Status\" = 'Served') = (\"ReceivedDate\" IS NOT NULL AND \"ServiceMode\" IS NOT NULL AND \"ProofReference\" IS NOT NULL AND \"AppealDeadline\" IS NOT NULL)");
            t.HasCheckConstraint("CK_NoticesOfAssessment_Cancelled", "(\"Status\" = 'Cancelled') = (\"CancelledAt\" IS NOT NULL)");
            t.HasCheckConstraint("CK_NoticesOfAssessment_Periods", "\"IssuePeriodDays\" > 0 AND \"AppealPeriodDays\" > 0");
        });
        builder.HasKey(x => x.Id);
        builder.HasOne<Taxpayer>().WithMany().HasForeignKey(x => x.AddresseeTaxpayerId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.NoticeNumber).HasMaxLength(100);
        builder.Property(x => x.Reason).HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.ServiceMode).HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.PreviousAssessedValue).HasPrecision(18, 2);
        builder.Property(x => x.AssessedValue).HasPrecision(18, 2);
        builder.Property(x => x.MarketValue).HasPrecision(18, 2);
        builder.Property(x => x.AddresseeNames).HasMaxLength(2000).IsRequired();
        builder.Property(x => x.AddresseeAddress).HasMaxLength(1000);
        builder.Property(x => x.ServedTo).HasMaxLength(300);
        builder.Property(x => x.ProofReference).HasMaxLength(200);
        builder.Property(x => x.ServiceNotes).HasMaxLength(1000);
        builder.Property(x => x.CancellationReason).HasMaxLength(1000);

        builder.HasOne(x => x.Property).WithMany().HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<RealPropertyUnit>().WithMany().HasForeignKey(x => x.RpuId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Assessment).WithMany().HasForeignKey(x => x.AssessmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<TaxDeclaration>().WithMany().HasForeignKey(x => x.TaxDeclarationId).OnDelete(DeleteBehavior.Restrict);

        // One live notice per assessment.
        builder.HasIndex(x => x.AssessmentId).IsUnique().HasFilter("\"Status\" <> 'Cancelled'").HasDatabaseName("UX_NoticesOfAssessment_Assessment_Live");
        builder.HasIndex(x => x.NoticeNumber).IsUnique();
        builder.HasIndex(x => new { x.PropertyId, x.Status });
    }
}

public sealed class NoticeOfAssessmentItemConfiguration : IEntityTypeConfiguration<NoticeOfAssessmentItem>
{
    public void Configure(EntityTypeBuilder<NoticeOfAssessmentItem> builder)
    {
        builder.ToTable("NoticeOfAssessmentItems", t => t.HasCheckConstraint("CK_NoticeOfAssessmentItems_Sequence", "\"Sequence\" >= 1"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Reason).HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.PreviousAssessedValue).HasPrecision(18, 2);
        builder.Property(x => x.AssessedValue).HasPrecision(18, 2);
        builder.Property(x => x.MarketValue).HasPrecision(18, 2);
        builder.HasOne<NoticeOfAssessment>().WithMany(x => x.Items).HasForeignKey(x => x.NoticeOfAssessmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PropertyEntity>().WithMany().HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Assessment).WithMany().HasForeignKey(x => x.AssessmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.NoticeOfAssessmentId, x.Sequence }).IsUnique();
        builder.HasIndex(x => x.AssessmentId);
    }
}

