using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Prime.Domain.Entities;
using Prime.Domain.Entities.Reference;
using Prime.Domain.Entities.Transactions;
using Prime.Infrastructure.Persistence.Configurations.Forms;

namespace Prime.Infrastructure.Persistence.Configurations.Transactions;

public sealed class TransactionTypeConfiguration : IEntityTypeConfiguration<TransactionType>
{
    public void Configure(EntityTypeBuilder<TransactionType> builder)
    {
        ConfigurationMapping.ConfigureCommon(builder, "TransactionTypes");
        builder.Property(x => x.Code).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Kind).HasConversion<string>().HasMaxLength(40);
        builder.Property(x => x.Description).HasMaxLength(1000);
        builder.HasMany(x => x.Requirements).WithOne().HasForeignKey(x => x.TransactionTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.Code).IsUnique().HasFilter(ConfigurationMapping.OpenApprovedFilter).HasDatabaseName("UX_TransactionTypes_OpenApproved");
    }
}

public sealed class TransactionTypeRequirementConfiguration : IEntityTypeConfiguration<TransactionTypeRequirement>
{
    public void Configure(EntityTypeBuilder<TransactionTypeRequirement> builder)
    {
        builder.ToTable("TransactionTypeRequirements");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Label).HasMaxLength(300).IsRequired();
        builder.Property(x => x.LegalBasis).HasMaxLength(500);
        builder.HasIndex(x => new { x.TransactionTypeId, x.Sequence }).IsUnique();
    }
}

public sealed class PropertyTransactionConfiguration : IEntityTypeConfiguration<PropertyTransaction>
{
    public void Configure(EntityTypeBuilder<PropertyTransaction> builder)
    {
        builder.ToTable("PropertyTransactions", t =>
        {
            t.HasCheckConstraint("CK_PropertyTransactions_Approved", "(\"Status\" = 'Approved') = (\"ApprovedAt\" IS NOT NULL)");
            t.HasCheckConstraint("CK_PropertyTransactions_Closed", "(\"Status\" IN ('Rejected', 'Cancelled')) = (\"ClosedAt\" IS NOT NULL)");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.TypeCode).HasMaxLength(20).IsRequired();
        builder.Property(x => x.TypeName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Kind).HasConversion<string>().HasMaxLength(40);
        builder.Property(x => x.TransactionNumber).HasMaxLength(100);
        builder.Property(x => x.Description).HasMaxLength(2000).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.CloseReason).HasMaxLength(1000);
        builder.HasOne(x => x.TransactionType).WithMany().HasForeignKey(x => x.TransactionTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Property).WithMany().HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Prime.Domain.Entities.RealPropertyUnit>().WithMany().HasForeignKey(x => x.TransferRpuId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.Requirements).WithOne().HasForeignKey(x => x.PropertyTransactionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.NewParties).WithOne().HasForeignKey(x => x.PropertyTransactionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.TdCancellations).WithOne().HasForeignKey(x => x.PropertyTransactionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.RelatedProperties).WithOne().HasForeignKey(x => x.PropertyTransactionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.TransactionNumber).IsUnique();
        builder.HasIndex(x => new { x.PropertyId, x.EffectiveDate });
        builder.HasIndex(x => x.Status);
    }
}

public sealed class PropertyTransactionRequirementConfiguration : IEntityTypeConfiguration<PropertyTransactionRequirement>
{
    public void Configure(EntityTypeBuilder<PropertyTransactionRequirement> builder)
    {
        builder.ToTable("PropertyTransactionRequirements", t =>
            t.HasCheckConstraint("CK_PropertyTransactionRequirements_Evidence", "\"SatisfiedAt\" IS NULL OR \"EvidenceReference\" IS NOT NULL"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Label).HasMaxLength(300).IsRequired();
        builder.Property(x => x.LegalBasis).HasMaxLength(500);
        builder.Property(x => x.EvidenceReference).HasMaxLength(200);
        builder.Property(x => x.Note).HasMaxLength(1000);
        builder.HasIndex(x => new { x.PropertyTransactionId, x.Sequence }).IsUnique();
    }
}

public sealed class PropertyTransactionPartyConfiguration : IEntityTypeConfiguration<PropertyTransactionParty>
{
    public void Configure(EntityTypeBuilder<PropertyTransactionParty> builder)
    {
        builder.ToTable("PropertyTransactionParties", t =>
        {
            t.HasCheckConstraint("CK_PropertyTransactionParties_UnknownOwner", "(\"Role\" = 'UnknownOwner') = (\"TaxpayerId\" IS NULL)");
            t.HasCheckConstraint("CK_PropertyTransactionParties_OwnershipType", "(\"Role\" = 'Owner') = (\"OwnershipTypeId\" IS NOT NULL)");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Role).HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.OwnershipPercentage).HasPrecision(9, 6);
        builder.HasOne(x => x.Taxpayer).WithMany().HasForeignKey(x => x.TaxpayerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<OwnershipType>().WithMany().HasForeignKey(x => x.OwnershipTypeId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class PropertyTransactionTdCancellationConfiguration : IEntityTypeConfiguration<PropertyTransactionTdCancellation>
{
    public void Configure(EntityTypeBuilder<PropertyTransactionTdCancellation> builder)
    {
        builder.ToTable("PropertyTransactionTdCancellations");
        builder.HasKey(x => x.Id);
        builder.HasOne(x => x.TaxDeclaration).WithMany().HasForeignKey(x => x.TaxDeclarationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.PropertyTransactionId, x.TaxDeclarationId }).IsUnique();
    }
}

public sealed class PropertyTransactionPropertyConfiguration : IEntityTypeConfiguration<PropertyTransactionProperty>
{
    public void Configure(EntityTypeBuilder<PropertyTransactionProperty> builder)
    {
        builder.ToTable("PropertyTransactionProperties");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Role).HasConversion<string>().HasMaxLength(20);
        builder.HasOne(x => x.Property).WithMany().HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.PropertyTransactionId, x.PropertyId }).IsUnique();
    }
}
