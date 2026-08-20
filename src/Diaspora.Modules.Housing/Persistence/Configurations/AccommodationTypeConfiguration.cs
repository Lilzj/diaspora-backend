using Diaspora.Modules.Housing.Domain.Catalog;
using Diaspora.Modules.Housing.Domain.Identifiers;
using Diaspora.Modules.Housing.Domain.Policies;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.Housing.Persistence.Configurations;

/// <summary>
/// Maps the accommodation-type catalogue.
/// <para>
/// <b>The unique index is on the key, not the name.</b> Listings store the key, two markets may
/// legitimately want the same display name for different things, and a contributor reseeding the
/// catalogue matches on the key. A unique name would refuse a perfectly reasonable second entry.
/// </para>
/// <para>
/// Not soft-deletable: a type is retired with a flag, because listings hold the key and a deleted
/// row would leave several thousand properties describing themselves as nothing.
/// </para>
/// </summary>
public sealed class AccommodationTypeConfiguration
    : IEntityTypeConfiguration<AccommodationType>
{
    public void Configure(EntityTypeBuilder<AccommodationType> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            HousingPersistenceConstants.AccommodationTypesTable,
            HousingModuleMetadata.Schema);

        builder.HasKey(type => type.Id);

        builder.Property(type => type.Id)
            .HasConversion(HousingValueConverters.Id<AccommodationTypeId>());

        builder.Property(type => type.Key)
            .HasMaxLength(HousingLimits.MaxTypeKey)
            .IsRequired();

        builder.Property(type => type.Name)
            .HasMaxLength(HousingLimits.MaxDisplayName)
            .IsRequired();

        builder.Property(type => type.Description).HasMaxLength(HousingLimits.MaxNoteLength);

        builder.Property(type => type.IconKey).HasMaxLength(HousingLimits.MaxDisplayName);

        builder.HasIndex(type => type.Key)
            .IsUnique()
            .HasDatabaseName("ix_housing_accommodation_types_key");

        builder.HasIndex(type => new { type.IsActive, type.Order })
            .HasDatabaseName("ix_housing_accommodation_types_active");

        builder.Ignore(type => type.DomainEvents);
    }
}
