using Diaspora.Modules.Location.Domain.Countries;
using Diaspora.Persistence;
using Diaspora.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.Location.Persistence.Configurations;

/// <summary>
/// Maps the <see cref="Country"/> aggregate.
/// <para>
/// The unique index on the ISO code is the one that matters. A country is addressed by code
/// everywhere in the platform — verification targets it, reputation is scored by it, listings
/// filter on it — and two rows for <c>GB</c> would split every one of those in a way that
/// looks like missing data rather than duplicated data.
/// </para>
/// </summary>
public sealed class CountryConfiguration : IEntityTypeConfiguration<Country>
{
    public void Configure(EntityTypeBuilder<Country> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(LocationPersistenceConstants.CountriesTable, LocationModuleMetadata.Schema);

        builder.HasKey(country => country.Id);

        builder.Property(country => country.Id)
            .HasConversion(LocationValueConverters.Id<CountryId>());

        builder.Property(country => country.Code)
            .HasConversion(LocationValueConverters.CountryCode)
            .HasMaxLength(LocationPersistenceConstants.CountryCodeLength)
            .IsRequired();

        builder.OwnsOne(country => country.Name, name =>
        {
            name.Property(value => value.Value)
                .HasColumnName("name")
                .HasMaxLength(LocationPersistenceConstants.NameLength)
                .IsRequired();

            name.Property(value => value.LocalName)
                .HasColumnName("local_name")
                .HasMaxLength(LocationPersistenceConstants.NameLength);
        });

        builder.Navigation(country => country.Name).IsRequired();

        builder.Property(country => country.Slug)
            .HasConversion(LocationValueConverters.Slug)
            .HasMaxLength(LocationPersistenceConstants.SlugLength)
            .IsRequired();

        ConfigureMetadata(builder);
        ConfigureGeography(builder);

        builder.Property(country => country.IsSupported).IsRequired();
        builder.Property(country => country.IsFeatured).IsRequired();

        builder.Property(country => country.LandmarkImageUrl)
            .HasMaxLength(LocationPersistenceConstants.UrlLength);

        // Packed lists. Mapped by backing-field name because the public surface is a computed
        // read-only projection — see Country for why they are packed rather than mapped as
        // collections.
        builder.Property<string?>("_spokenLanguages")
            .HasColumnName("spoken_languages")
            .HasMaxLength(LocationPersistenceConstants.PackedListLength);

        builder.Property<string?>("_timeZones")
            .HasColumnName("time_zones")
            .HasMaxLength(LocationPersistenceConstants.PackedListLength);

        builder.Property(country => country.CreatedAt).IsRequired();

        builder.HasIndex(country => country.Code)
            .IsUnique()
            .HasDatabaseName("ix_countries_code");

        builder.HasIndex(country => country.Slug)
            .IsUnique()
            .HasDatabaseName("ix_countries_slug");

        // The country-selection screens sort featured-first then alphabetically, and the
        // supported filter is the most common predicate on the list. One index serves both.
        builder.HasIndex(country => new { country.IsSupported, country.IsFeatured })
            .HasDatabaseName("ix_countries_supported_featured");

        builder.Ignore(country => country.DomainEvents);
        builder.Ignore(country => country.Path);
        builder.Ignore(country => country.Flag);
        builder.Ignore(country => country.SpokenLanguages);
        builder.Ignore(country => country.TimeZones);
    }

    private static void ConfigureMetadata(EntityTypeBuilder<Country> builder)
    {
        builder.OwnsOne(country => country.Metadata, metadata =>
        {
            metadata.Property(value => value.CallingCode)
                .HasConversion(LocationValueConverters.CallingCode)
                .HasColumnName("calling_code")
                .HasMaxLength(LocationPersistenceConstants.CallingCodeLength);

            metadata.Property(value => value.Currency)
                .HasConversion(LocationValueConverters.CurrencyCode)
                .HasColumnName("currency_code")
                .HasMaxLength(LocationPersistenceConstants.CurrencyCodeLength);

            metadata.Property(value => value.PrimaryLanguage)
                .HasConversion(LocationValueConverters.LanguageCode)
                .HasColumnName("primary_language")
                .HasMaxLength(LocationPersistenceConstants.LanguageCodeLength);

            metadata.Property(value => value.DefaultLocale)
                .HasConversion(LocationValueConverters.Locale)
                .HasColumnName("default_locale")
                .HasMaxLength(LocationPersistenceConstants.LocaleLength);

            metadata.Property(value => value.PrimaryTimeZone)
                .HasConversion(LocationValueConverters.TimeZone)
                .HasColumnName("primary_time_zone")
                .HasMaxLength(LocationPersistenceConstants.TimeZoneLength);

            metadata.Property(value => value.Continent)
                .HasColumnName("continent")
                .HasMaxLength(LocationPersistenceConstants.ContinentLength);

            metadata.Property(value => value.Subregion)
                .HasColumnName("subregion")
                .HasMaxLength(LocationPersistenceConstants.ContinentLength);
        });

        builder.Navigation(country => country.Metadata).IsRequired();
    }

    private static void ConfigureGeography(EntityTypeBuilder<Country> builder)
    {
        builder.OwnsOne(country => country.Center, center =>
        {
            center.Property(value => value.Latitude).HasColumnName("center_latitude");
            center.Property(value => value.Longitude).HasColumnName("center_longitude");
        });

        builder.OwnsOne(country => country.Extent, extent =>
        {
            extent.Property(value => value.North).HasColumnName("extent_north");
            extent.Property(value => value.South).HasColumnName("extent_south");
            extent.Property(value => value.East).HasColumnName("extent_east");
            extent.Property(value => value.West).HasColumnName("extent_west");
        });
    }
}
