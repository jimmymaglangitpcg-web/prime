using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Prime.Domain.Entities;
using Prime.Infrastructure.Persistence.Configurations.Forms;

namespace Prime.Infrastructure.Persistence.Configurations;

/// <summary>General revision programmes (docs/analysis/smv-preparation-general-revision.md §4.6).</summary>
public sealed class GeneralRevisionProgrammeConfiguration : IEntityTypeConfiguration<GeneralRevisionProgramme>
{
    public void Configure(EntityTypeBuilder<GeneralRevisionProgramme> builder)
    {
        builder.ToTable("GeneralRevisionProgrammes", t =>
        {
            t.HasCheckConstraint("CK_GeneralRevisionProgrammes_Completed", "(\"Status\" = 'Completed') = (\"CompletedAt\" IS NOT NULL)");
            t.HasCheckConstraint("CK_GeneralRevisionProgrammes_Cancelled", "(\"Status\" = 'Cancelled') = (\"CancellationReason\" IS NOT NULL)");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.OfficeOrderReference).HasMaxLength(200);
        builder.Property(x => x.OrdinanceReference).HasMaxLength(200);
        builder.Property(x => x.Description).HasMaxLength(1000);
        builder.Property(x => x.CancellationReason).HasMaxLength(1000);
        builder.HasOne(x => x.Smv).WithMany().HasForeignKey(x => x.SmvId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.Scope).WithOne().HasForeignKey(x => x.GeneralRevisionProgrammeId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(x => x.Suspensions).WithOne().HasForeignKey(x => x.GeneralRevisionProgrammeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.RevisionYear);
    }
}

public sealed class GeneralRevisionScopeConfiguration : IEntityTypeConfiguration<GeneralRevisionScope>
{
    public void Configure(EntityTypeBuilder<GeneralRevisionScope> builder)
    {
        builder.ToTable("GeneralRevisionScopes");
        builder.HasKey(x => x.Id);
        builder.HasOne(x => x.Municipality).WithMany().HasForeignKey(x => x.MunicipalityId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.GeneralRevisionProgrammeId, x.MunicipalityId }).IsUnique();
    }
}

public sealed class GeneralRevisionSuspensionConfiguration : IEntityTypeConfiguration<GeneralRevisionSuspension>
{
    public void Configure(EntityTypeBuilder<GeneralRevisionSuspension> builder)
    {
        builder.ToTable("GeneralRevisionSuspensions", t => t.HasCheckConstraint("CK_GeneralRevisionSuspensions_Dates",
            "(\"UntilDate\" IS NULL OR \"UntilDate\" >= \"FromDate\") AND (\"LiftedOn\" IS NULL OR \"LiftedOn\" >= \"FromDate\")"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Kind).HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.Reference).HasMaxLength(300).IsRequired();
        builder.Property(x => x.Remarks).HasMaxLength(1000);
    }
}

public sealed class GeneralRevisionItemConfiguration : IEntityTypeConfiguration<GeneralRevisionItem>
{
    public void Configure(EntityTypeBuilder<GeneralRevisionItem> builder)
    {
        builder.ToTable("GeneralRevisionItems", t =>
        {
            t.HasCheckConstraint("CK_GeneralRevisionItems_Failed", "(\"Status\" = 'Failed') = (\"FailureReason\" IS NOT NULL)");
            t.HasCheckConstraint("CK_GeneralRevisionItems_Excluded", "(\"Status\" = 'Excluded') = (\"ExclusionReason\" IS NOT NULL)");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Pin).HasMaxLength(100).IsRequired();
        builder.Property(x => x.RpuNumber).HasMaxLength(100).IsRequired();
        builder.Property(x => x.RpuType).HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.FailureReason).HasMaxLength(1000);
        builder.Property(x => x.PreviousMarketValue).HasPrecision(18, 2);
        builder.Property(x => x.PreviousAssessedValue).HasPrecision(18, 2);
        builder.Property(x => x.NewMarketValue).HasPrecision(18, 2);
        builder.Property(x => x.NewAssessedValue).HasPrecision(18, 2);
        builder.Property(x => x.InspectionRoute).HasMaxLength(100);
        builder.Property(x => x.InspectionNotes).HasMaxLength(2000);
        builder.Property(x => x.ExclusionReason).HasMaxLength(1000);

        builder.HasOne(x => x.GeneralRevisionProgramme).WithMany().HasForeignKey(x => x.GeneralRevisionProgrammeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Rpu).WithMany().HasForeignKey(x => x.RpuId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Property).WithMany().HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.PreviousAssessment).WithMany().HasForeignKey(x => x.PreviousAssessmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Assessment).WithMany().HasForeignKey(x => x.AssessmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Valuation>().WithMany().HasForeignKey(x => x.ValuationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<GeneralRevisionJob>().WithMany().HasForeignKey(x => x.LastRunId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Prime.Domain.Entities.Reference.Municipality>().WithMany().HasForeignKey(x => x.MunicipalityId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Prime.Domain.Entities.Reference.Barangay>().WithMany().HasForeignKey(x => x.BarangayId).OnDelete(DeleteBehavior.Restrict);

        // A unit is in a programme once.
        builder.HasIndex(x => new { x.GeneralRevisionProgrammeId, x.RpuId }).IsUnique();
        builder.HasIndex(x => new { x.GeneralRevisionProgrammeId, x.Status });
        builder.HasIndex(x => new { x.GeneralRevisionProgrammeId, x.BarangayId, x.Pin });
        builder.HasOne<Prime.Domain.Entities.Identity.AppUser>().WithMany().HasForeignKey(x => x.InspectorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.GeneralRevisionProgrammeId, x.InspectorId });
    }
}

public sealed class GeneralRevisionRunIssueConfiguration : IEntityTypeConfiguration<GeneralRevisionRunIssue>
{
    public void Configure(EntityTypeBuilder<GeneralRevisionRunIssue> builder)
    {
        builder.ToTable("GeneralRevisionRunIssues");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Pin).HasMaxLength(100).IsRequired();
        builder.Property(x => x.RpuNumber).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Code).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Message).HasMaxLength(1000).IsRequired();
        builder.Property(x => x.Failed).HasDefaultValue(true);
        builder.HasOne<GeneralRevisionJob>().WithMany().HasForeignKey(x => x.GeneralRevisionJobId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<GeneralRevisionItem>().WithMany().HasForeignKey(x => x.GeneralRevisionItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.GeneralRevisionJobId, x.Pin });
    }
}

public sealed class GeneralRevisionChecklistStepDefinitionConfiguration : IEntityTypeConfiguration<GeneralRevisionChecklistStepDefinition>
{
    public void Configure(EntityTypeBuilder<GeneralRevisionChecklistStepDefinition> builder)
    {
        ConfigurationMapping.ConfigureCommon(builder, "GeneralRevisionChecklistStepDefinitions", t =>
            t.HasCheckConstraint("CK_GeneralRevisionChecklistStepDefinitions_Sequence", "\"Sequence\" > 0"));
        builder.Property(x => x.Code).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Title).HasMaxLength(300).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(2000);
        builder.Property(x => x.Gate).HasConversion<string>().HasMaxLength(40);
        builder.HasIndex(x => x.Code).IsUnique().HasFilter(ConfigurationMapping.OpenApprovedFilter).HasDatabaseName("UX_GeneralRevisionChecklistStepDefinitions_OpenApproved");
        builder.HasIndex(x => new { x.Code, x.EffectiveDate });
    }
}

public sealed class GeneralRevisionChecklistStepConfiguration : IEntityTypeConfiguration<GeneralRevisionChecklistStep>
{
    public void Configure(EntityTypeBuilder<GeneralRevisionChecklistStep> builder)
    {
        builder.ToTable("GeneralRevisionChecklistSteps", t => t.HasCheckConstraint("CK_GeneralRevisionChecklistSteps_Manual",
            "\"Gate\" IS NULL OR \"CompletedOn\" IS NULL"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Title).HasMaxLength(300).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(2000);
        builder.Property(x => x.Gate).HasConversion<string>().HasMaxLength(40);
        builder.Property(x => x.Evidence).HasMaxLength(500);
        builder.HasOne<GeneralRevisionProgramme>().WithMany().HasForeignKey(x => x.GeneralRevisionProgrammeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<GeneralRevisionChecklistStepDefinition>().WithMany().HasForeignKey(x => x.DefinitionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.GeneralRevisionProgrammeId, x.Code }).IsUnique();
    }
}
