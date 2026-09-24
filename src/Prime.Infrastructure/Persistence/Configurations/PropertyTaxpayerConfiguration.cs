using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Prime.Domain.Entities;
using Prime.Domain.Enums;

namespace Prime.Infrastructure.Persistence.Configurations;

public sealed class PropertyTaxpayerConfiguration : IEntityTypeConfiguration<PropertyTaxpayer>
{
    public void Configure(EntityTypeBuilder<PropertyTaxpayer> builder)
    {
        builder.ToTable(t =>
        {
            // LGC §§204–205 (docs/FORMS-REVISION-PLAN.md A4): only an unknown owner has no taxpayer,
            // only owners carry an ownership type, and an unknown owner holds no share.
            t.HasCheckConstraint("CK_PropertyTaxpayers_UnknownOwner", "(\"Role\" = 'UnknownOwner') = (\"TaxpayerId\" IS NULL)");
            t.HasCheckConstraint("CK_PropertyTaxpayers_OwnershipType", "(\"Role\" = 'Owner') = (\"OwnershipTypeId\" IS NOT NULL)");
            t.HasCheckConstraint("CK_PropertyTaxpayers_Share", "\"OwnershipPercentage\" >= 0 AND \"OwnershipPercentage\" <= 100 AND (\"Role\" <> 'UnknownOwner' OR \"OwnershipPercentage\" = 0)");
            t.HasCheckConstraint("CK_PropertyTaxpayers_Ended", "\"IsCurrent\" OR \"EndDate\" IS NOT NULL");
        });
        builder.HasKey(x => x.Id);

        // Existing rows are owners.
        builder.Property(x => x.Role).HasConversion<string>().HasMaxLength(30).HasDefaultValue(PropertyPartyRole.Owner).HasSentinel(PropertyPartyRole.Owner);
        builder.Property(x => x.EndReason).HasMaxLength(500);
        builder.Property(x => x.OwnershipPercentage).HasPrecision(9, 6);
        // At most one current unknown-owner declaration per property.
        builder.HasIndex(x => x.PropertyId).IsUnique().HasFilter("\"Role\" = 'UnknownOwner' AND \"IsCurrent\"")
            .HasDatabaseName("UX_PropertyTaxpayers_CurrentUnknownOwner");

        builder.HasOne(x => x.Property).WithMany().HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Taxpayer).WithMany().HasForeignKey(x => x.TaxpayerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.OwnershipType).WithMany().HasForeignKey(x => x.OwnershipTypeId).OnDelete(DeleteBehavior.Restrict);

        // Effective-date pattern — docs/DATABASE.md §4: fast "who currently
        // owns this property" and "as of a past date" lookups.
        builder.HasIndex(x => new { x.PropertyId, x.IsCurrent });
        builder.HasIndex(x => new { x.PropertyId, x.StartDate });
        builder.HasIndex(x => x.TaxpayerId);
    }
}
