using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Prime.Domain.Entities.Audit;

namespace Prime.Infrastructure.Persistence.Configurations.Audit;

public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Module).HasMaxLength(100).IsRequired();
        builder.Property(x => x.TableName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Action).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.IpAddress).HasMaxLength(45); // IPv6 max length
        builder.Property(x => x.Reason).HasMaxLength(1000);

        // OldValue/NewValue are unbounded JSON snapshots.
        builder.Property(x => x.OldValue).HasColumnType("jsonb");
        builder.Property(x => x.NewValue).HasColumnType("jsonb");

        // Primary access pattern: "audit history for this record".
        builder.HasIndex(x => new { x.TableName, x.RecordId });
        builder.HasIndex(x => x.Timestamp);
        builder.HasIndex(x => x.UserId);

        // No FK to AppUser — audit rows must survive even if the acting
        // user's profile is later altered, and (per Phase 3 decision)
        // CreatedBy-style columns are not hard-FK'd yet either.
    }
}
