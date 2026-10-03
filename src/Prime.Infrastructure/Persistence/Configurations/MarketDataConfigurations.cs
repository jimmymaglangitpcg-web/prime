using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Prime.Domain.Entities.MarketData;

namespace Prime.Infrastructure.Persistence.Configurations;

/// <summary>Market data (docs/analysis/smv-preparation-general-revision.md §4.1).</summary>
public sealed class MarketTransactionConfiguration : IEntityTypeConfiguration<MarketTransaction>
{
    public void Configure(EntityTypeBuilder<MarketTransaction> builder)
    {
        builder.ToTable("MarketTransactions", t =>
        {
            t.HasCheckConstraint("CK_MarketTransactions_Amounts",
                "\"Consideration\" >= 0 AND COALESCE(\"LandConsideration\", 0) >= 0 AND COALESCE(\"LandConsideration\", 0) <= \"Consideration\""
                + " AND COALESCE(\"LandArea\", 0) >= 0 AND COALESCE(\"BuildingFloorArea\", 0) >= 0");
            t.HasCheckConstraint("CK_MarketTransactions_Conveys", "\"ConveysLand\" OR \"ConveysBuilding\"");
            t.HasCheckConstraint("CK_MarketTransactions_Excluded", "(\"Review\" = 'Excluded') = (\"ExclusionReason\" IS NOT NULL)");
            t.HasCheckConstraint("CK_MarketTransactions_Reviewed", "(\"Review\" = 'Unreviewed') = (\"ReviewedAt\" IS NULL)");
            t.HasCheckConstraint("CK_MarketTransactions_Cancelled", "(\"CancelledAt\" IS NULL) = (\"CancellationReason\" IS NULL)");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Source).HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.DocumentReference).HasMaxLength(200);
        builder.Property(x => x.DocumentFileNumber).HasMaxLength(100);
        builder.Property(x => x.GrantorNames).HasMaxLength(1000);
        builder.Property(x => x.GranteeNames).HasMaxLength(1000);
        builder.Property(x => x.GranteeAddress).HasMaxLength(1000);
        builder.Property(x => x.Location).HasMaxLength(300);
        builder.Property(x => x.Pin).HasMaxLength(100);
        builder.Property(x => x.TaxDeclarationNumber).HasMaxLength(100);
        builder.Property(x => x.LotNumber).HasMaxLength(100);
        builder.Property(x => x.PreviousTitleNumber).HasMaxLength(100);
        builder.Property(x => x.NewTitleNumber).HasMaxLength(100);
        builder.Property(x => x.LandArea).HasPrecision(18, 4);
        builder.Property(x => x.LandAreaUnit).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.BuildingFloorArea).HasPrecision(18, 4);
        builder.Property(x => x.Consideration).HasPrecision(18, 2);
        builder.Property(x => x.LandConsideration).HasPrecision(18, 2);
        builder.Property(x => x.LandUnitPrice).HasPrecision(18, 2);
        builder.Property(x => x.BuildingUnitPrice).HasPrecision(18, 2);
        builder.Property(x => x.Review).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.ExclusionReason).HasMaxLength(500);
        builder.Property(x => x.ReviewNote).HasMaxLength(1000);
        builder.Property(x => x.ImportBatch).HasMaxLength(100);
        builder.Property(x => x.Remarks).HasMaxLength(1000);
        builder.Property(x => x.CancellationReason).HasMaxLength(1000);

        builder.HasOne(x => x.ConveyanceMode).WithMany().HasForeignKey(x => x.ConveyanceModeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Municipality).WithMany().HasForeignKey(x => x.MunicipalityId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Barangay).WithMany().HasForeignKey(x => x.BarangayId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Property).WithMany().HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Classification).WithMany().HasForeignKey(x => x.ClassificationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.SubClassification).WithMany().HasForeignKey(x => x.SubClassificationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ActualUse).WithMany().HasForeignKey(x => x.ActualUseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.BuildingType).WithMany().HasForeignKey(x => x.BuildingTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.StructuralType).WithMany().HasForeignKey(x => x.StructuralTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Prime.Domain.Entities.Transactions.PropertyTransaction>().WithMany().HasForeignKey(x => x.PropertyTransactionId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.MunicipalityId, x.TransactionDate });
        builder.HasIndex(x => new { x.ClassificationId, x.SubClassificationId });
        builder.HasIndex(x => x.PropertyId);
        // One prefilled record per transfer.
        builder.HasIndex(x => x.PropertyTransactionId).IsUnique().HasFilter("\"PropertyTransactionId\" IS NOT NULL");
    }
}

public sealed class BuildingPermitAbstractConfiguration : IEntityTypeConfiguration<BuildingPermitAbstract>
{
    public void Configure(EntityTypeBuilder<BuildingPermitAbstract> builder)
    {
        builder.ToTable("BuildingPermitAbstracts", t =>
        {
            t.HasCheckConstraint("CK_BuildingPermitAbstracts_Amounts",
                "COALESCE(\"Storeys\", 1) >= 1 AND COALESCE(\"TotalFloorArea\", 0) >= 0 AND COALESCE(\"EstimatedCost\", 0) >= 0");
            t.HasCheckConstraint("CK_BuildingPermitAbstracts_Cancelled", "(\"CancelledAt\" IS NULL) = (\"CancellationReason\" IS NULL)");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.PermitNumber).HasMaxLength(100).IsRequired();
        builder.Property(x => x.PermitteeName).HasMaxLength(300).IsRequired();
        builder.Property(x => x.PermitteeAddress).HasMaxLength(1000);
        builder.Property(x => x.TaxDeclarationNumber).HasMaxLength(100);
        builder.Property(x => x.BlockLotNumber).HasMaxLength(100);
        builder.Property(x => x.Street).HasMaxLength(300);
        builder.Property(x => x.Scope).HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.TotalFloorArea).HasPrecision(18, 4);
        builder.Property(x => x.EstimatedCost).HasPrecision(18, 2);
        builder.Property(x => x.Remarks).HasMaxLength(1000);
        builder.Property(x => x.CancellationReason).HasMaxLength(1000);

        builder.HasOne(x => x.Municipality).WithMany().HasForeignKey(x => x.MunicipalityId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Barangay).WithMany().HasForeignKey(x => x.BarangayId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.BuildingType).WithMany().HasForeignKey(x => x.BuildingTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.StructuralType).WithMany().HasForeignKey(x => x.StructuralTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Classification).WithMany().HasForeignKey(x => x.ClassificationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Building).WithMany().HasForeignKey(x => x.BuildingId).OnDelete(DeleteBehavior.Restrict);

        // A permit is recorded once per municipality (the issuing office's numbering).
        builder.HasIndex(x => new { x.MunicipalityId, x.PermitNumber }).IsUnique().HasFilter("\"CancelledAt\" IS NULL");
        builder.HasIndex(x => x.BuildingId);
    }
}

public sealed class MachineryRegistrationAbstractConfiguration : IEntityTypeConfiguration<MachineryRegistrationAbstract>
{
    public void Configure(EntityTypeBuilder<MachineryRegistrationAbstract> builder)
    {
        builder.ToTable("MachineryRegistrationAbstracts", t =>
        {
            t.HasCheckConstraint("CK_MachineryRegistrationAbstracts_Cost", "COALESCE(\"Cost\", 0) >= 0");
            t.HasCheckConstraint("CK_MachineryRegistrationAbstracts_Cancelled", "(\"CancelledAt\" IS NULL) = (\"CancellationReason\" IS NULL)");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.CertificateNumber).HasMaxLength(100).IsRequired();
        builder.Property(x => x.OwnerName).HasMaxLength(300).IsRequired();
        builder.Property(x => x.OwnerAddress).HasMaxLength(1000);
        builder.Property(x => x.TaxDeclarationNumber).HasMaxLength(100);
        builder.Property(x => x.Location).HasMaxLength(300);
        builder.Property(x => x.Description).HasMaxLength(500);
        builder.Property(x => x.BrandModel).HasMaxLength(200);
        builder.Property(x => x.Manufacturer).HasMaxLength(200);
        builder.Property(x => x.Cost).HasPrecision(18, 2);
        builder.Property(x => x.CurrentCondition).HasMaxLength(200);
        builder.Property(x => x.Remarks).HasMaxLength(1000);
        builder.Property(x => x.CancellationReason).HasMaxLength(1000);

        builder.HasOne(x => x.Municipality).WithMany().HasForeignKey(x => x.MunicipalityId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Barangay).WithMany().HasForeignKey(x => x.BarangayId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.MachineryType).WithMany().HasForeignKey(x => x.MachineryTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Machinery).WithMany().HasForeignKey(x => x.MachineryId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.MunicipalityId, x.CertificateNumber }).IsUnique().HasFilter("\"CancelledAt\" IS NULL");
        builder.HasIndex(x => x.MachineryId);
    }
}

public sealed class MarketDataReportRunConfiguration : IEntityTypeConfiguration<MarketDataReportRun>
{
    public void Configure(EntityTypeBuilder<MarketDataReportRun> builder)
    {
        builder.ToTable("MarketDataReportRuns", t => t.HasCheckConstraint("CK_MarketDataReportRuns_Period", "\"FromDate\" <= \"ToDate\""));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Kind).HasConversion<string>().HasMaxLength(40);
        builder.Property(x => x.Remarks).HasMaxLength(1000);
        builder.HasOne(x => x.Municipality).WithMany().HasForeignKey(x => x.MunicipalityId).OnDelete(DeleteBehavior.Restrict);
    }
}
