using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Prime.Domain.Common;
using Prime.Domain.Entities.Forms;
using Prime.Domain.Entities.Workflow;

namespace Prime.Infrastructure.Persistence.Configurations.Forms;

/// <summary>Shared mapping for effective-dated configuration tables (docs/FORMS-REVISION-PLAN.md §4).</summary>
internal static class ConfigurationMapping
{
    /// <summary>"One open approved version per scope" — the approval-time supersession, guaranteed by the database too.</summary>
    public const string OpenApprovedFilter = "\"Status\" = 'Approved' AND \"EndDate\" IS NULL";

    public static void ConfigureCommon<T>(EntityTypeBuilder<T> builder, string table, Action<TableBuilder<T>>? checks = null)
        where T : EffectiveDatedConfiguration
    {
        builder.ToTable(table, t =>
        {
            t.HasCheckConstraint($"CK_{table}_EndDate", "\"EndDate\" IS NULL OR \"EndDate\" >= \"EffectiveDate\"");
            t.HasCheckConstraint($"CK_{table}_Approval", "(\"Status\" IN ('Approved', 'Posted')) = (\"ApprovedAt\" IS NOT NULL)");
            checks?.Invoke(t);
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.LegalBasis).HasMaxLength(500).IsRequired();
        builder.Property(x => x.Remarks).HasMaxLength(1000);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        builder.HasIndex(x => new { x.Status, x.EffectiveDate });
    }
}

public sealed class NumberingSchemeConfiguration : IEntityTypeConfiguration<NumberingScheme>
{
    public void Configure(EntityTypeBuilder<NumberingScheme> builder)
    {
        ConfigurationMapping.ConfigureCommon(builder, "NumberingSchemes");
        builder.Property(x => x.AppliesTo).HasConversion<string>().HasMaxLength(40);
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Pattern).HasMaxLength(200).IsRequired();
        builder.Property(x => x.ValidationRegex).HasMaxLength(500);
        builder.HasIndex(x => x.AppliesTo).IsUnique().HasFilter(ConfigurationMapping.OpenApprovedFilter)
            .HasDatabaseName("UX_NumberingSchemes_OpenApproved");
    }
}

public sealed class NumberSequenceConfiguration : IEntityTypeConfiguration<NumberSequence>
{
    public void Configure(EntityTypeBuilder<NumberSequence> builder)
    {
        builder.ToTable("NumberSequences", t => t.HasCheckConstraint("CK_NumberSequences_LastValue", "\"LastValue\" >= 0"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ScopeKey).HasMaxLength(200).IsRequired();
        builder.HasOne<NumberingScheme>().WithMany().HasForeignKey(x => x.NumberingSchemeId).OnDelete(DeleteBehavior.Restrict);
        // The allocator's INSERT … ON CONFLICT targets this index.
        builder.HasIndex(x => new { x.NumberingSchemeId, x.ScopeKey }).IsUnique();
    }
}

public sealed class FormDefinitionConfiguration : IEntityTypeConfiguration<FormDefinition>
{
    public void Configure(EntityTypeBuilder<FormDefinition> builder)
    {
        ConfigurationMapping.ConfigureCommon(builder, "FormDefinitions",
            t => t.HasCheckConstraint("CK_FormDefinitions_Version", "\"Version\" >= 1"));
        builder.Property(x => x.Code).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Title).HasMaxLength(200).IsRequired();
        builder.Property(x => x.SubjectType).HasConversion<string>().HasMaxLength(40);
        builder.Property(x => x.Authority).HasConversion<string>().HasMaxLength(40);
        builder.Property(x => x.SourceReference).HasMaxLength(500);
        builder.Property(x => x.TemplateBody).IsRequired();
        builder.HasIndex(x => new { x.Code, x.Version }).IsUnique();
        builder.HasIndex(x => x.Code).IsUnique().HasFilter(ConfigurationMapping.OpenApprovedFilter)
            .HasDatabaseName("UX_FormDefinitions_OpenApproved");
    }
}

public sealed class IssuedFormConfiguration : IEntityTypeConfiguration<IssuedForm>
{
    public void Configure(EntityTypeBuilder<IssuedForm> builder)
    {
        builder.ToTable("IssuedForms", t =>
        {
            t.HasCheckConstraint("CK_IssuedForms_Status", "\"Status\" IN ('Posted', 'Cancelled')");
            t.HasCheckConstraint("CK_IssuedForms_Cancelled", "(\"Status\" = 'Cancelled') = (\"CancelledAt\" IS NOT NULL)");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.FormCode).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Authority).HasConversion<string>().HasMaxLength(40);
        builder.Property(x => x.SubjectType).HasConversion<string>().HasMaxLength(40);
        builder.Property(x => x.DocumentNumber).HasMaxLength(100);
        builder.Property(x => x.DataSnapshotJson).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.RenderedHtml).IsRequired();
        builder.Property(x => x.RenderedHtmlSha256).HasMaxLength(64).IsFixedLength().IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.CancellationReason).HasMaxLength(1000);
        builder.HasOne(x => x.FormDefinition).WithMany().HasForeignKey(x => x.FormDefinitionId).OnDelete(DeleteBehavior.Restrict);

        // Idempotent issuance: one valid issue per subject and form version.
        builder.HasIndex(x => new { x.FormDefinitionId, x.SubjectId }).IsUnique()
            .HasFilter("\"Status\" = 'Posted'").HasDatabaseName("UX_IssuedForms_Definition_Subject_Valid");
        builder.HasIndex(x => new { x.SubjectType, x.SubjectId });
    }
}

public sealed class ApprovalChainConfiguration : IEntityTypeConfiguration<ApprovalChain>
{
    public void Configure(EntityTypeBuilder<ApprovalChain> builder)
    {
        ConfigurationMapping.ConfigureCommon(builder, "ApprovalChains");
        builder.Property(x => x.SubjectType).HasConversion<string>().HasMaxLength(40);
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.HasMany(x => x.Steps).WithOne().HasForeignKey(x => x.ApprovalChainId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.SubjectType).IsUnique().HasFilter(ConfigurationMapping.OpenApprovedFilter)
            .HasDatabaseName("UX_ApprovalChains_OpenApproved");
    }
}

public sealed class ApprovalChainStepConfiguration : IEntityTypeConfiguration<ApprovalChainStep>
{
    public void Configure(EntityTypeBuilder<ApprovalChainStep> builder)
    {
        builder.ToTable("ApprovalChainSteps", t => t.HasCheckConstraint("CK_ApprovalChainSteps_Sequence", "\"Sequence\" >= 1"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.StepCode).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Label).HasMaxLength(200).IsRequired();
        builder.Property(x => x.SignatoryPosition).HasMaxLength(200);
        builder.HasIndex(x => new { x.ApprovalChainId, x.Sequence }).IsUnique();
    }
}

public sealed class ApprovalRecordConfiguration : IEntityTypeConfiguration<ApprovalRecord>
{
    public void Configure(EntityTypeBuilder<ApprovalRecord> builder)
    {
        builder.ToTable("ApprovalRecords");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.SubjectType).HasConversion<string>().HasMaxLength(40);
        builder.Property(x => x.StepCode).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Label).HasMaxLength(200).IsRequired();
        builder.Property(x => x.SignatoryPosition).HasMaxLength(200);
        builder.Property(x => x.SignatoryName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Remarks).HasMaxLength(1000);
        builder.HasOne<ApprovalChain>().WithMany().HasForeignKey(x => x.ApprovalChainId).OnDelete(DeleteBehavior.Restrict);
        // Each step is signed once per record; concurrent signers of the same step conflict here.
        builder.HasIndex(x => new { x.SubjectType, x.SubjectId, x.StepSequence }).IsUnique();
    }
}
