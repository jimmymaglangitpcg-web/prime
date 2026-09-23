using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Prime.Domain.Entities;

namespace Prime.Infrastructure.Persistence.Configurations;

public sealed class SmvConfiguration : IEntityTypeConfiguration<Smv>
{
    public void Configure(EntityTypeBuilder<Smv> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.OrdinanceNumber).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(1000);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);

        builder.HasIndex(x => x.OrdinanceNumber).IsUnique();
        builder.HasIndex(x => x.EffectivityDate);
    }
}
