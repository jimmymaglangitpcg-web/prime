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
        builder.Property(x => x.ParentTableName).HasMaxLength(100);
        builder.Property(x => x.Action).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.IpAddress).HasMaxLength(45); // IPv6 max length
        builder.Property(x => x.Reason).HasMaxLength(1000);

        // OldValue/NewValue are unbounded JSON snapshots.
        builder.Property(x => x.OldValue).HasColumnType("jsonb");
        builder.Property(x => x.NewValue).HasColumnType("jsonb");

        // Primary access pattern: "audit history for this record".
        builder.HasIndex(x => new { x.TableName, x.RecordId });
        // One table's trail in the viewer's exact order (newest first, then id). Without it the newest-first scan of
        // IX_AuditLogs_Timestamp wades through every newer row of other tables: past 30 s on page 100 once a general revision
        // had added millions. An index on (TableName, Timestamp) alone still loses the planner's estimate to that scan
        // (production-hardening.md §9, H4).
        builder.HasIndex(x => new { x.TableName, x.Timestamp, x.Id }).IsDescending(false, true, false)
            .HasDatabaseName("IX_AuditLogs_TableName_Timestamp_Id");
        builder.HasIndex(x => x.Timestamp);
        builder.HasIndex(x => x.UserId);
        // The audit viewer (docs/analysis/workflow-security.md §4.3): a user's activity, and a record's child rows.
        builder.HasIndex(x => new { x.UserId, x.Timestamp });
        builder.HasIndex(x => x.ParentRecordId);
        // A record's or a property's history looks rows up by record id alone; (TableName, RecordId) cannot serve that.
        builder.HasIndex(x => x.RecordId);

        // No FK to AppUser — audit rows must survive even if the acting
        // user's profile is later altered, and (per Phase 3 decision)
        // CreatedBy-style columns are not hard-FK'd yet either.
    }
}
