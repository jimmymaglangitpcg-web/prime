using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Prime.Domain.Entities.Reference;

namespace Prime.Infrastructure.Persistence.Configurations.Reference;

/// <summary>
/// Shared configuration for the identically-shaped LGU-configurable lookup
/// tables (Zone, Classification, ActualUse, ...). Each concrete
/// <see cref="IEntityTypeConfiguration{TEntity}"/> below just applies this
/// and, if needed, adds type-specific rules.
/// </summary>
public abstract class LookupEntityConfiguration<T> : IEntityTypeConfiguration<T>
    where T : LookupEntity
{
    public virtual void Configure(EntityTypeBuilder<T> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Code).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(1000);

        builder.HasIndex(x => x.Code).IsUnique();

        builder.Property(x => x.CreatedAt).IsRequired();
    }
}

public sealed class ZoneConfiguration : LookupEntityConfiguration<Zone>;
public sealed class ClassificationConfiguration : LookupEntityConfiguration<Classification>;
public sealed class ActualUseConfiguration : LookupEntityConfiguration<ActualUse>;
public sealed class SubClassificationConfiguration : LookupEntityConfiguration<SubClassification>;
public sealed class RoadTypeConfiguration : LookupEntityConfiguration<RoadType>;
public sealed class ConditionConfiguration : LookupEntityConfiguration<Condition>;
public sealed class BuildingTypeConfiguration : LookupEntityConfiguration<BuildingType>;
public sealed class StructuralTypeConfiguration : LookupEntityConfiguration<StructuralType>;
public sealed class BuildingComponentTypeConfiguration : LookupEntityConfiguration<BuildingComponentType>;
public sealed class MachineryTypeConfiguration : LookupEntityConfiguration<MachineryType>;
public sealed class OwnershipTypeConfiguration : LookupEntityConfiguration<OwnershipType>;
public sealed class DocumentTypeConfiguration : LookupEntityConfiguration<DocumentType>;
public sealed class PropertyTypeConfiguration : LookupEntityConfiguration<PropertyType>;
public sealed class ImprovementKindConfiguration : LookupEntityConfiguration<ImprovementKind>;
public sealed class TitleTypeConfiguration : LookupEntityConfiguration<TitleType>;
public sealed class StructuralPartConfiguration : LookupEntityConfiguration<StructuralPart>;

public sealed class StructuralMaterialConfiguration : LookupEntityConfiguration<StructuralMaterial>
{
    public override void Configure(EntityTypeBuilder<StructuralMaterial> builder)
    {
        base.Configure(builder);
        builder.HasOne(x => x.StructuralPart).WithMany().HasForeignKey(x => x.StructuralPartId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.StructuralPartId);
    }
}
