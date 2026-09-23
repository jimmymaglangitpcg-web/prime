using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Prime.Domain.Entities;

namespace Prime.Infrastructure.Persistence.Configurations;

public sealed class GeneralRevisionJobConfiguration : IEntityTypeConfiguration<GeneralRevisionJob>
{
    public void Configure(EntityTypeBuilder<GeneralRevisionJob> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Remarks).HasMaxLength(2000);

        builder.HasIndex(x => x.RevisionYear);
        builder.HasIndex(x => x.Status);
    }
}
