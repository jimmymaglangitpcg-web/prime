using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Prime.Domain.Entities;
using Prime.Domain.Entities.SwornStatements;

namespace Prime.Infrastructure.Persistence.Configurations;

public sealed class SwornStatementConfiguration : IEntityTypeConfiguration<SwornStatement>
{
    public void Configure(EntityTypeBuilder<SwornStatement> builder)
    {
        builder.ToTable("SwornStatements", t =>
        {
            // A filed (or later superseded) statement was sworn, signed and received (docs/analysis/mrpaao-forms-model.md §16.3).
            t.HasCheckConstraint("CK_SwornStatements_Filed",
                "(\"Status\" IN ('Filed', 'Superseded')) <= (\"FiledAt\" IS NOT NULL AND \"SignedOn\" IS NOT NULL AND \"SwornOn\" IS NOT NULL"
                + " AND \"AdministeringOfficer\" IS NOT NULL AND \"ReceivedOn\" IS NOT NULL)");
            t.HasCheckConstraint("CK_SwornStatements_Cancelled", "(\"Status\" = 'Cancelled') = (\"CancelledAt\" IS NOT NULL)");
            t.HasCheckConstraint("CK_SwornStatements_Owners", "\"Capacity\" = 'Owner' OR \"OwnerNames\" IS NOT NULL");
            t.HasCheckConstraint("CK_SwornStatements_Witnesses", "NOT \"Thumbmarked\" OR \"Status\" = 'Draft' OR \"Status\" = 'Cancelled'"
                + " OR (\"Witness1\" IS NOT NULL AND \"Witness2\" IS NOT NULL)");
            t.HasCheckConstraint("CK_SwornStatements_NotSelf", "\"SupersedesId\" IS NULL OR \"SupersedesId\" <> \"Id\"");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Number).HasMaxLength(100);
        builder.Property(x => x.DeclarantName).HasMaxLength(300).IsRequired();
        builder.Property(x => x.Citizenship).HasMaxLength(100);
        builder.Property(x => x.CivilStatus).HasMaxLength(50);
        builder.Property(x => x.PostalAddress).HasMaxLength(1000);
        builder.Property(x => x.DeclarantTin).HasMaxLength(50);
        builder.Property(x => x.Capacity).HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.OwnerNames).HasMaxLength(2000);
        builder.Property(x => x.FilingBasis).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.SignedAt).HasMaxLength(300);
        builder.Property(x => x.Witness1).HasMaxLength(300);
        builder.Property(x => x.Witness2).HasMaxLength(300);
        builder.Property(x => x.SwornAt).HasMaxLength(300);
        builder.Property(x => x.AdministeringOfficer).HasMaxLength(300);
        builder.Property(x => x.OfficerTin).HasMaxLength(50);
        builder.Property(x => x.IdentityDocument).HasMaxLength(300);
        builder.Property(x => x.IdentityDocumentIssuedAt).HasMaxLength(300);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.CancellationReason).HasMaxLength(1000);
        builder.Property(x => x.Remarks).HasMaxLength(1000);

        builder.HasOne(x => x.DeclarantTaxpayer).WithMany().HasForeignKey(x => x.DeclarantTaxpayerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Municipality).WithMany().HasForeignKey(x => x.MunicipalityId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Supersedes).WithMany().HasForeignKey(x => x.SupersedesId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.Number).IsUnique();
        // One live correction per statement.
        builder.HasIndex(x => x.SupersedesId).IsUnique().HasFilter("\"SupersedesId\" IS NOT NULL AND \"Status\" <> 'Cancelled'")
            .HasDatabaseName("UX_SwornStatements_Supersedes_Live");
        builder.HasIndex(x => new { x.MunicipalityId, x.Status });
        builder.HasIndex(x => x.DeclarantName);
        builder.HasIndex(x => x.ReceivedOn);
    }
}

public sealed class SwornStatementItemConfiguration : IEntityTypeConfiguration<SwornStatementItem>
{
    public void Configure(EntityTypeBuilder<SwornStatementItem> builder)
    {
        builder.ToTable("SwornStatementItems", t =>
        {
            t.HasCheckConstraint("CK_SwornStatementItems_Sequence", "\"Sequence\" >= 1");
            t.HasCheckConstraint("CK_SwornStatementItems_Value", "\"DeclaredMarketValue\" >= 0");
            // An existing declaration is a TD in PRIME or a number PRIME does not hold, never both.
            t.HasCheckConstraint("CK_SwornStatementItems_ExistingTd", "\"TaxDeclarationId\" IS NULL OR \"ExistingTdNumber\" IS NULL");
            t.HasCheckConstraint("CK_SwornStatementItems_Unit", "\"RpuId\" IS NULL OR \"PropertyId\" IS NOT NULL");
            // The columns each kind of Att. 11 requires.
            t.HasCheckConstraint("CK_SwornStatementItems_Kind",
                "(\"Kind\" = 'Land' AND \"Area\" > 0 AND \"AreaUnit\" IS NOT NULL)"
                + " OR (\"Kind\" = 'Building' AND \"FloorArea\" > 0)"
                + " OR (\"Kind\" = 'Machinery' AND \"Description\" IS NOT NULL)"
                + " OR (\"Kind\" = 'OtherImprovement' AND \"ImprovementKindId\" IS NOT NULL)");
            t.HasCheckConstraint("CK_SwornStatementItems_Amounts",
                "COALESCE(\"AcquisitionCost\", 0) >= 0 AND COALESCE(\"InstallationCost\", 0) >= 0 AND COALESCE(\"Depreciation\", 0) >= 0"
                + " AND COALESCE(\"ProductiveCount\", 0) >= 0 AND COALESCE(\"NonProductiveCount\", 0) >= 0 AND COALESCE(\"Storeys\", 1) >= 1");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Kind).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.ExistingTdNumber).HasMaxLength(100);
        builder.Property(x => x.Location).HasMaxLength(500);
        builder.Property(x => x.DeclaredMarketValue).HasPrecision(18, 2);
        builder.Property(x => x.LotNumber).HasMaxLength(100);
        builder.Property(x => x.BlockNumber).HasMaxLength(100);
        builder.Property(x => x.CadastralNumber).HasMaxLength(100);
        builder.Property(x => x.TitleNumber).HasMaxLength(100);
        builder.Property(x => x.Area).HasPrecision(18, 4);
        builder.Property(x => x.AreaUnit).HasMaxLength(20);
        builder.Property(x => x.FloorArea).HasPrecision(18, 4);
        builder.Property(x => x.Description).HasMaxLength(500);
        builder.Property(x => x.LotOwnerName).HasMaxLength(300);
        builder.Property(x => x.AcquisitionCost).HasPrecision(18, 2);
        builder.Property(x => x.InstallationCost).HasPrecision(18, 2);
        builder.Property(x => x.Depreciation).HasPrecision(18, 2);
        builder.Property(x => x.AnnualProduct).HasMaxLength(200);
        builder.Property(x => x.Ages).HasMaxLength(200);

        builder.HasOne<SwornStatement>().WithMany(x => x.Items).HasForeignKey(x => x.SwornStatementId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.TaxDeclaration).WithMany().HasForeignKey(x => x.TaxDeclarationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PropertyEntity>().WithMany().HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Rpu).WithMany().HasForeignKey(x => x.RpuId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Classification).WithMany().HasForeignKey(x => x.ClassificationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ActualUse).WithMany().HasForeignKey(x => x.ActualUseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ImprovementKind).WithMany().HasForeignKey(x => x.ImprovementKindId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.SwornStatementId, x.Sequence }).IsUnique();
        builder.HasIndex(x => x.PropertyId);
        builder.HasIndex(x => x.RpuId);
        builder.HasIndex(x => x.TaxDeclarationId);
        builder.HasIndex(x => x.ExistingTdNumber);
    }
}
