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

        builder.Property(x => x.Mode).HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.Reason).HasMaxLength(1000);
        builder.HasOne(x => x.GeneralRevisionProgramme).WithMany().HasForeignKey(x => x.GeneralRevisionProgrammeId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.RevisionYear);
        builder.HasIndex(x => x.Status);
    }
}
