using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Prime.Domain.Entities;

namespace Prime.Infrastructure.Persistence.Configurations;

public sealed class PropertyTaxpayerConfiguration : IEntityTypeConfiguration<PropertyTaxpayer>
{
    public void Configure(EntityTypeBuilder<PropertyTaxpayer> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.OwnershipPercentage).HasPrecision(9, 6);

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
