using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Prime.Domain.Entities.Offices;
using Prime.Infrastructure.Persistence.Configurations.Forms;

namespace Prime.Infrastructure.Persistence.Configurations.Offices;

public sealed class OfficeConfiguration : IEntityTypeConfiguration<Office>
{
    public void Configure(EntityTypeBuilder<Office> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(50).IsRequired();
        builder.HasIndex(x => x.Code).IsUnique();
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Kind).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.LguName).HasMaxLength(200);
        builder.Property(x => x.HeadPosition).HasMaxLength(200);
        builder.Property(x => x.SanggunianName).HasMaxLength(200);
        builder.Property(x => x.Address).HasMaxLength(500);
        builder.Property(x => x.Contact).HasMaxLength(200);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        // The province has exactly one provincial office (§3.1).
        builder.HasIndex(x => x.Kind).IsUnique().HasFilter("\"Kind\" = 'Provincial'").HasDatabaseName("UX_Offices_OneProvincial");
    }
}

public sealed class OfficeJurisdictionConfiguration : IEntityTypeConfiguration<OfficeJurisdiction>
{
    public void Configure(EntityTypeBuilder<OfficeJurisdiction> builder)
    {
        ConfigurationMapping.ConfigureCommon(builder, "OfficeJurisdictions");
        builder.HasOne(x => x.Office).WithMany().HasForeignKey(x => x.OfficeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Municipality).WithMany().HasForeignKey(x => x.MunicipalityId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.OfficeId);
        // One office per municipality at a time: the approval-time supersession, guaranteed by the database too.
        builder.HasIndex(x => x.MunicipalityId).IsUnique().HasFilter(ConfigurationMapping.OpenApprovedFilter)
            .HasDatabaseName("UX_OfficeJurisdictions_OpenApproved");
    }
}

public sealed class OfficeAssignmentConfiguration : IEntityTypeConfiguration<OfficeAssignment>
{
    public void Configure(EntityTypeBuilder<OfficeAssignment> builder)
    {
        ConfigurationMapping.ConfigureCommon(builder, "OfficeAssignments");
        builder.HasOne(x => x.AppUser).WithMany().HasForeignKey(x => x.AppUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Office).WithMany().HasForeignKey(x => x.OfficeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.Roles).WithOne().HasForeignKey(x => x.OfficeAssignmentId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => x.OfficeId);
        // One office assignment per user at a time (Q2).
        builder.HasIndex(x => x.AppUserId).IsUnique().HasFilter(ConfigurationMapping.OpenApprovedFilter)
            .HasDatabaseName("UX_OfficeAssignments_OpenApproved");
    }
}

public sealed class ApprovalDelegationConfiguration : IEntityTypeConfiguration<ApprovalDelegation>
{
    public void Configure(EntityTypeBuilder<ApprovalDelegation> builder)
    {
        builder.ToTable("ApprovalDelegations", t =>
        {
            t.HasCheckConstraint("CK_ApprovalDelegations_Period", "\"ValidTo\" >= \"ValidFrom\"");
            t.HasCheckConstraint("CK_ApprovalDelegations_Approval", "(\"Status\" = 'Approved') = (\"ApprovedAt\" IS NOT NULL)");
            t.HasCheckConstraint("CK_ApprovalDelegations_Subjects", "cardinality(\"SubjectTypes\") > 0");
            t.HasCheckConstraint("CK_ApprovalDelegations_Revocation",
                "(\"RevokedFrom\" IS NULL) = (\"RevokedAt\" IS NULL) AND (\"RevokedFrom\" IS NULL OR \"Status\" = 'Approved')");
        });
        builder.HasKey(x => x.Id);
        builder.HasOne(x => x.Office).WithMany().HasForeignKey(x => x.OfficeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApprovalDelegation>().WithMany().HasForeignKey(x => x.RenewsDelegationId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.DelegatingOfficialName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.DelegatingOfficialPosition).HasMaxLength(200).IsRequired();
        builder.Property(x => x.InstrumentReference).HasMaxLength(300).IsRequired();
        // Stored as text arrays of the enum names, readable in the database.
        builder.PrimitiveCollection(x => x.SubjectTypes).ElementType(e => e.HasConversion<string>());
        builder.PrimitiveCollection(x => x.PropertyKinds).ElementType(e => e.HasConversion<string>());
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.RejectionReason).HasMaxLength(1000);
        builder.Property(x => x.RevocationReason).HasMaxLength(1000);
        builder.Property(x => x.Remarks).HasMaxLength(1000);
        builder.HasIndex(x => new { x.OfficeId, x.Status, x.ValidFrom });
    }
}

public sealed class OfficeAssignmentRoleConfiguration : IEntityTypeConfiguration<OfficeAssignmentRole>
{
    public void Configure(EntityTypeBuilder<OfficeAssignmentRole> builder)
    {
        builder.HasKey(x => x.Id);
        builder.HasOne(x => x.Role).WithMany().HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.OfficeAssignmentId, x.RoleId }).IsUnique();
    }
}
