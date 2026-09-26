using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Prime.Domain.Entities.Registers;

namespace Prime.Infrastructure.Persistence.Configurations;

public sealed class RegisterRunConfiguration : IEntityTypeConfiguration<RegisterRun>
{
    public void Configure(EntityTypeBuilder<RegisterRun> builder)
    {
        builder.ToTable("RegisterRuns", t =>
        {
            t.HasCheckConstraint("CK_RegisterRuns_Period", "\"FromDate\" IS NULL OR \"FromDate\" <= \"AsOf\"");
            // Each kind carries its scope (docs/analysis/mrpaao-forms-model.md §15).
            t.HasCheckConstraint("CK_RegisterRuns_Scope",
                "(\"Kind\" = 'OwnershipRecordCard' AND \"TaxpayerId\" IS NOT NULL) OR (\"Kind\" <> 'OwnershipRecordCard' AND \"BarangayId\" IS NOT NULL)");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Kind).HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.Remarks).HasMaxLength(1000);
        builder.HasOne(x => x.Barangay).WithMany().HasForeignKey(x => x.BarangayId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Classification).WithMany().HasForeignKey(x => x.ClassificationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Taxpayer).WithMany().HasForeignKey(x => x.TaxpayerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.Kind, x.CreatedAt });
    }
}
