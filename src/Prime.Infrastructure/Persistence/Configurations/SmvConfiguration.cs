using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Prime.Domain.Entities;

namespace Prime.Infrastructure.Persistence.Configurations;

public sealed class SmvConfiguration : IEntityTypeConfiguration<Smv>
{
    public void Configure(EntityTypeBuilder<Smv> builder)
    {
        // An ordinance SMV names its ordinance; a certified SMV its certification (docs/analysis/valuation-foundation.md §4.3).
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_Smvs_OrdinanceBasis", "\"Basis\" <> 'Ordinance' OR (\"OrdinanceNumber\" IS NOT NULL AND \"OrdinanceDate\" IS NOT NULL)");
            t.HasCheckConstraint("CK_Smvs_CertifiedBasis", "\"Basis\" <> 'Certified' OR \"CertificationReference\" IS NOT NULL");
        });
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Basis).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.OrdinanceNumber).HasMaxLength(50);
        builder.Property(x => x.CertificationReference).HasMaxLength(100);
        builder.Property(x => x.PublicationReference).HasMaxLength(200);
        builder.Property(x => x.Description).HasMaxLength(1000);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        builder.Ignore(x => x.Reference);
        builder.HasMany(x => x.Coverage).WithOne().HasForeignKey(x => x.SmvId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.OrdinanceNumber).IsUnique();
        builder.HasIndex(x => x.CertificationReference).IsUnique();
        builder.HasIndex(x => x.EffectivityDate);
    }
}

public sealed class SmvCoverageConfiguration : IEntityTypeConfiguration<SmvCoverage>
{
    public void Configure(EntityTypeBuilder<SmvCoverage> builder)
    {
        builder.ToTable("SmvCoverages");
        builder.HasKey(x => x.Id);
        builder.HasOne(x => x.Municipality).WithMany().HasForeignKey(x => x.MunicipalityId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.SmvId, x.MunicipalityId }).IsUnique();
    }
}
