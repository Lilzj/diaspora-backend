using Diaspora.Modules.Housing.Domain.Catalog;
using Diaspora.Modules.Housing.Domain.Identifiers;
using Diaspora.Modules.Housing.Domain.Policies;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.Housing.Persistence.Configurations;

/// <summary>
/// Maps the amenity catalogue.
/// <para>
/// The index on the accessibility flag exists because that filter is not a convenience: for the
/// members who need step-free access it decides whether a property is habitable at all, and it
/// must not depend on how a deployment happened to group its amenities.
/// </para>
/// </summary>
public sealed class HousingAmenityConfiguration : IEntityTypeConfiguration<HousingAmenity>
{
    public void Configure(EntityTypeBuilder<HousingAmenity> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            HousingPersistenceConstants.AmenitiesTable, HousingModuleMetadata.Schema);

        builder.HasKey(amenity => amenity.Id);

        builder.Property(amenity => amenity.Id)
            .HasConversion(HousingValueConverters.Id<HousingAmenityId>());

        builder.Property(amenity => amenity.Key)
            .HasMaxLength(HousingLimits.MaxAmenityKey)
            .IsRequired();

        builder.Property(amenity => amenity.Name)
            .HasMaxLength(HousingLimits.MaxDisplayName)
            .IsRequired();

        builder.Property(amenity => amenity.Group).HasMaxLength(HousingLimits.MaxDisplayName);

        builder.Property(amenity => amenity.IconKey).HasMaxLength(HousingLimits.MaxDisplayName);

        builder.HasIndex(amenity => amenity.Key)
            .IsUnique()
            .HasDatabaseName("ix_housing_amenities_key");

        builder.HasIndex(amenity => new { amenity.IsActive, amenity.Group, amenity.Order })
            .HasDatabaseName("ix_housing_amenities_active_group");

        builder.HasIndex(amenity => amenity.IsAccessibilityFeature)
            .HasDatabaseName("ix_housing_amenities_accessibility");

        builder.Ignore(amenity => amenity.DomainEvents);
    }
}
