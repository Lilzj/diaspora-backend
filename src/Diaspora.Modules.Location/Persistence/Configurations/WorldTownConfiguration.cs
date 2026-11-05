using Diaspora.Modules.Location.Domain.Towns;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.Location.Persistence.Configurations;

public sealed class WorldTownConfiguration : IEntityTypeConfiguration<WorldTown>
{
    public void Configure(EntityTypeBuilder<WorldTown> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(LocationPersistenceConstants.WorldTownsTable, LocationModuleMetadata.Schema);

        builder.HasKey(town => town.Id);

        builder.Property(town => town.Id)
            .HasConversion(LocationValueConverters.Id<WorldTownId>());

        builder.Property(town => town.GeoNameId).IsRequired();

        builder.Property(town => town.Name)
            .HasMaxLength(LocationPersistenceConstants.TownNameLength)
            .IsRequired();

        builder.Property(town => town.CountryCode)
            .HasMaxLength(LocationPersistenceConstants.CountryCodeLength)
            .IsRequired();

        builder.Property(town => town.Latitude).IsRequired();
        builder.Property(town => town.Longitude).IsRequired();
        builder.Property(town => town.Population).IsRequired();

        builder.Property(town => town.SearchNames)
            .HasMaxLength(LocationPersistenceConstants.TownSearchNamesLength)
            .IsRequired();

        builder.HasIndex(town => town.GeoNameId)
            .IsUnique()
            .HasDatabaseName("ix_world_towns_geoname");

        // Most lookups name the member's own country first.
        builder.HasIndex(town => new { town.CountryCode, town.Population })
            .HasDatabaseName("ix_world_towns_country_population");

        builder.Ignore(town => town.Names);
        builder.Ignore(town => town.DomainEvents);
    }
}
