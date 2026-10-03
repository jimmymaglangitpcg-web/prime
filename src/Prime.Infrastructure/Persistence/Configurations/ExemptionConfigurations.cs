using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Prime.Domain.Entities;
using Prime.Domain.Entities.Exemptions;
using Prime.Infrastructure.Persistence.Configurations.Forms;

namespace Prime.Infrastructure.Persistence.Configurations;

public sealed class ExemptionTypeConfiguration : IEntityTypeConfiguration<ExemptionType>
{
    public void Configure(EntityTypeBuilder<ExemptionType> builder)
    {
        ConfigurationMapping.ConfigureCommon(builder, "ExemptionTypes", t =>
        {
            t.HasCheckConstraint("CK_ExemptionTypes_AppliesTo", "\"AppliesTo\" BETWEEN 1 AND 15");
            t.HasCheckConstraint("CK_ExemptionTypes_Ceiling", "\"AssessedValueCeiling\" IS NULL OR \"AssessedValueCeiling\" > 0");
        });
        builder.Property(x => x.Code).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(1000);
        builder.Property(x => x.AssessedValueCeiling).HasPrecision(18, 2);
        builder.HasIndex(x => x.Code).IsUnique().HasFilter(ConfigurationMapping.OpenApprovedFilter).HasDatabaseName("UX_ExemptionTypes_OpenApproved");
        builder.HasIndex(x => new { x.Code, x.EffectiveDate });
    }
}

public sealed class PropertyExemptionConfiguration : IEntityTypeConfiguration<PropertyExemption>
{
    public void Configure(EntityTypeBuilder<PropertyExemption> builder)
    {
        builder.ToTable("PropertyExemptions", t =>
        {
            t.HasCheckConstraint("CK_PropertyExemptions_ProofDue", "\"ProofDueDate\" >= \"ClaimedOn\"");
            t.HasCheckConstraint("CK_PropertyExemptions_Decided",
                "(\"Status\" IN ('Approved', 'Rejected', 'Ended')) = (\"DecidedAt\" IS NOT NULL)");
            t.HasCheckConstraint("CK_PropertyExemptions_Approved",
                "\"Status\" NOT IN ('Approved', 'Ended') OR \"EffectiveDate\" IS NOT NULL");
            t.HasCheckConstraint("CK_PropertyExemptions_Expiry", "\"ExpiryDate\" IS NULL OR \"ExpiryDate\" >= \"EffectiveDate\"");
            t.HasCheckConstraint("CK_PropertyExemptions_Ended", "(\"Status\" = 'Ended') = (\"EndedOn\" IS NOT NULL AND \"EndReason\" IS NOT NULL)");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.PortionDescription).HasMaxLength(500);
        builder.Property(x => x.Reference).HasMaxLength(500);
        builder.Property(x => x.Remarks).HasMaxLength(1000);
        builder.Property(x => x.DecisionRemarks).HasMaxLength(1000);
        builder.Property(x => x.EndReason).HasMaxLength(1000);
        builder.HasOne(x => x.Property).WithMany().HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Rpu).WithMany().HasForeignKey(x => x.RpuId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ExemptionType).WithMany().HasForeignKey(x => x.ExemptionTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ActualUse).WithMany().HasForeignKey(x => x.ActualUseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ClaimantTaxpayer).WithMany().HasForeignKey(x => x.ClaimantTaxpayerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.Evidence).WithOne().HasForeignKey(x => x.PropertyExemptionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Assessment>().WithMany().HasForeignKey(x => x.ReassessmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.RpuId, x.Status });
        builder.HasIndex(x => new { x.Status, x.ProofDueDate });
        builder.HasIndex(x => x.PropertyId);
    }
}

public sealed class ExemptionEvidenceConfiguration : IEntityTypeConfiguration<ExemptionEvidence>
{
    public void Configure(EntityTypeBuilder<ExemptionEvidence> builder)
    {
        builder.ToTable("ExemptionEvidence", t => t.HasCheckConstraint("CK_ExemptionEvidence_Sequence", "\"Sequence\" >= 1"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Description).HasMaxLength(500).IsRequired();
        builder.Property(x => x.ReferenceNumber).HasMaxLength(100);
        builder.HasIndex(x => new { x.PropertyExemptionId, x.Sequence }).IsUnique();
    }
}

public sealed class AssessmentLevelCeilingConfiguration : IEntityTypeConfiguration<AssessmentLevelCeiling>
{
    public void Configure(EntityTypeBuilder<AssessmentLevelCeiling> builder)
    {
        ConfigurationMapping.ConfigureCommon(builder, "AssessmentLevelCeilings", t =>
        {
            t.HasCheckConstraint("CK_AssessmentLevelCeilings_Percentage", "\"MaximumPercentage\" > 0 AND \"MaximumPercentage\" <= 100");
            t.HasCheckConstraint("CK_AssessmentLevelCeilings_Bracket", "\"LowerValue\" >= 0 AND (\"UpperValue\" IS NULL OR \"UpperValue\" > \"LowerValue\")");
        });
        builder.Property(x => x.Code).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(1000);
        builder.Property(x => x.LowerValue).HasPrecision(18, 2);
        builder.Property(x => x.UpperValue).HasPrecision(18, 2);
        builder.Property(x => x.MaximumPercentage).HasPrecision(9, 6);
        builder.HasOne(x => x.PropertyType).WithMany().HasForeignKey(x => x.PropertyTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Classification).WithMany().HasForeignKey(x => x.ClassificationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ActualUse).WithMany().HasForeignKey(x => x.ActualUseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.Code).IsUnique().HasFilter(ConfigurationMapping.OpenApprovedFilter).HasDatabaseName("UX_AssessmentLevelCeilings_OpenApproved");
        builder.HasIndex(x => new { x.Code, x.EffectiveDate });
        builder.HasIndex(x => new { x.PropertyTypeId, x.EffectiveDate });
    }
}
