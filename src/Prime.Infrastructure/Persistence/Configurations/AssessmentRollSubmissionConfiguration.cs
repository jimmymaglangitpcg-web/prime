using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Prime.Domain.Entities.Registers;

namespace Prime.Infrastructure.Persistence.Configurations;

public sealed class AssessmentRollSubmissionConfiguration : IEntityTypeConfiguration<AssessmentRollSubmission>
{
    public void Configure(EntityTypeBuilder<AssessmentRollSubmission> builder)
    {
        builder.ToTable("AssessmentRollSubmissions", t =>
        {
            t.HasCheckConstraint("CK_AssessmentRollSubmissions_Month", "\"Month\" BETWEEN 1 AND 12");
            t.HasCheckConstraint("CK_AssessmentRollSubmissions_Reviewed",
                "(\"Status\" = 'Submitted') = (\"ReviewedAt\" IS NULL) AND (\"Status\" <> 'Returned' OR \"ReviewRemarks\" IS NOT NULL)");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Remarks).HasMaxLength(1000);
        builder.Property(x => x.ReviewRemarks).HasMaxLength(1000);
        builder.HasOne(x => x.Municipality).WithMany().HasForeignKey(x => x.MunicipalityId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Office).WithMany().HasForeignKey(x => x.OfficeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.Items).WithOne().HasForeignKey(x => x.SubmissionId).OnDelete(DeleteBehavior.Restrict);
        // One submission in play per municipality and month; a returned one makes room for its replacement.
        builder.HasIndex(x => new { x.MunicipalityId, x.Year, x.Month }).IsUnique()
            .HasFilter("\"Status\" <> 'Returned'").HasDatabaseName("UX_AssessmentRollSubmissions_Month");
    }
}

public sealed class AssessmentRollSubmissionItemConfiguration : IEntityTypeConfiguration<AssessmentRollSubmissionItem>
{
    public void Configure(EntityTypeBuilder<AssessmentRollSubmissionItem> builder)
    {
        builder.ToTable("AssessmentRollSubmissionItems", t =>
            t.HasCheckConstraint("CK_AssessmentRollSubmissionItems_Kind", "\"Kind\" IN ('AssessmentRollTaxable', 'AssessmentRollExempt')"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Kind).HasConversion<string>().HasMaxLength(30);
        builder.HasOne(x => x.RegisterRun).WithMany().HasForeignKey(x => x.RegisterRunId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.IssuedForm).WithMany().HasForeignKey(x => x.IssuedFormId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Barangay).WithMany().HasForeignKey(x => x.BarangayId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.RegisterRunId).IsUnique();
    }
}
