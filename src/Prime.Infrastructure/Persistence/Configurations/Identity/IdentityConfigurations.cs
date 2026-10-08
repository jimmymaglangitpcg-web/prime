using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Prime.Domain.Entities.Identity;

namespace Prime.Infrastructure.Persistence.Configurations.Identity;

public sealed class AppUserConfiguration : IEntityTypeConfiguration<AppUser>
{
    public void Configure(EntityTypeBuilder<AppUser> builder)
    {
        builder.HasKey(x => x.Id);

        // Value only — deliberately not a DB foreign key to auth.users,
        // which only exists inside a Supabase project's own database, not
        // the local dev Postgres. See docs/DATABASE.md §1.
        builder.Property(x => x.SupabaseUserId).IsRequired();
        builder.HasIndex(x => x.SupabaseUserId).IsUnique();

        builder.Property(x => x.DisplayName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.ReaLicenceNumber).HasMaxLength(50);
        builder.Property(x => x.Email).HasMaxLength(320).IsRequired();
        builder.HasIndex(x => x.Email);

        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
    }
}

public sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.HasIndex(x => x.Code).IsUnique();
    }
}

public sealed class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Module).HasMaxLength(100).IsRequired();
        builder.HasIndex(x => x.Code).IsUnique();
    }
}

public sealed class UserRoleConfiguration : IEntityTypeConfiguration<UserRole>
{
    public void Configure(EntityTypeBuilder<UserRole> builder)
    {
        builder.HasKey(x => new { x.AppUserId, x.RoleId });

        builder.HasOne(x => x.AppUser).WithMany(x => x.UserRoles).HasForeignKey(x => x.AppUserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Role).WithMany(x => x.UserRoles).HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> builder)
    {
        builder.HasKey(x => new { x.RoleId, x.PermissionId });

        builder.HasOne(x => x.Role).WithMany(x => x.RolePermissions).HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Permission).WithMany(x => x.RolePermissions).HasForeignKey(x => x.PermissionId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class RolePermissionChangeConfiguration : IEntityTypeConfiguration<RolePermissionChange>
{
    public void Configure(EntityTypeBuilder<RolePermissionChange> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Reason).HasMaxLength(500).IsRequired();
        builder.Property(x => x.DecisionReason).HasMaxLength(500);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        builder.HasOne(x => x.Role).WithMany().HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Restrict);
        // One open proposal per role at a time, so two drafts cannot undo each other.
        builder.HasIndex(x => x.RoleId).IsUnique().HasFilter("\"Status\" = 'Draft'").HasDatabaseName("UX_RolePermissionChanges_OpenDraft");
    }
}


public sealed class SignUpRequestConfiguration : IEntityTypeConfiguration<SignUpRequest>
{
    public void Configure(EntityTypeBuilder<SignUpRequest> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.FullName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Position).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Note).HasMaxLength(1000);
        builder.Property(x => x.DecisionReason).HasMaxLength(500);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        builder.HasOne(x => x.AppUser).WithMany().HasForeignKey(x => x.AppUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.RequestedOffice).WithMany().HasForeignKey(x => x.RequestedOfficeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Prime.Domain.Entities.Offices.OfficeAssignment>().WithMany().HasForeignKey(x => x.OfficeAssignmentId).OnDelete(DeleteBehavior.Restrict);
        // One open request per user (workflow-security.md §4.2).
        builder.HasIndex(x => x.AppUserId).IsUnique().HasFilter("\"Status\" = 'PendingReview'").HasDatabaseName("UX_SignUpRequests_Open");
        builder.HasIndex(x => new { x.Status, x.CreatedAt });
    }
}

public sealed class UserStatusChangeConfiguration : IEntityTypeConfiguration<UserStatusChange>
{
    public void Configure(EntityTypeBuilder<UserStatusChange> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Reason).HasMaxLength(500).IsRequired();
        builder.Property(x => x.DecisionReason).HasMaxLength(500);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.NewStatus).HasConversion<string>().HasMaxLength(20);
        builder.HasOne(x => x.AppUser).WithMany().HasForeignKey(x => x.AppUserId).OnDelete(DeleteBehavior.Restrict);
        // One open proposal per user, so two drafts cannot undo each other (Q6).
        builder.HasIndex(x => x.AppUserId).IsUnique().HasFilter("\"Status\" = 'Draft'").HasDatabaseName("UX_UserStatusChanges_OpenDraft");
    }
}
