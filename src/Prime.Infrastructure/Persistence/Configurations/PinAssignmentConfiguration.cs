using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Prime.Domain.Entities;

namespace Prime.Infrastructure.Persistence.Configurations;

public sealed class PinAssignmentConfiguration : IEntityTypeConfiguration<PinAssignment>
{
    public void Configure(EntityTypeBuilder<PinAssignment> builder)
    {
        builder.ToTable("PinAssignments", t =>
        {
            t.HasCheckConstraint("CK_PinAssignments_Retired", "(\"RetiredAt\" IS NULL) = (\"RetirementReason\" IS NULL)");
            t.HasCheckConstraint("CK_PinAssignments_Permanent",
                "\"Kind\" <> 'Permanent' OR (\"ParcelId\" IS NOT NULL AND \"SectionId\" IS NOT NULL AND \"ParcelNumber\" IS NOT NULL)");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Pin).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Kind).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.RetirementReason).HasMaxLength(1000);
        builder.HasOne(x => x.Property).WithMany().HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Parcel).WithMany().HasForeignKey(x => x.ParcelId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Barangay).WithMany().HasForeignKey(x => x.BarangayId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Section).WithMany().HasForeignKey(x => x.SectionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Prime.Domain.Entities.Transactions.PropertyTransaction>().WithMany().HasForeignKey(x => x.PropertyTransactionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Prime.Domain.Entities.Transactions.PropertyTransaction>().WithMany().HasForeignKey(x => x.AssignedByTransactionId).OnDelete(DeleteBehavior.Restrict);
        // A PIN is given once, ever (MRPAAO Ch. II §1 D.4).
        builder.HasIndex(x => x.Pin).IsUnique();
        // One current PIN per property.
        builder.HasIndex(x => x.PropertyId).IsUnique().HasFilter("\"RetiredAt\" IS NULL").HasDatabaseName("UX_PinAssignments_Current");
        builder.HasIndex(x => x.BarangayId);
        builder.HasIndex(x => x.SectionId);
    }
}
