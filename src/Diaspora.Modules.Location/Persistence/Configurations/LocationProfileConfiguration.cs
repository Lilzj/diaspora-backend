using Diaspora.Modules.Location.Domain.Profiles;
using Diaspora.Persistence;
using Diaspora.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.Location.Persistence.Configurations;

/// <summary>
/// Maps the <see cref="LocationProfile"/> aggregate and its two owned collections.
/// <para>
/// Saved and recent locations are configured as owned collections rather than as their own
/// aggregates because they have no life outside the profile: nothing references a saved
/// location by identifier, nothing queries recents across members, and both are always loaded
/// and saved with the profile. Owning them makes the cascade delete free and the invariants —
/// five recents, twenty-five saves — enforceable in one place.
/// </para>
/// </summary>
public sealed class LocationProfileConfiguration : IEntityTypeConfiguration<LocationProfile>
{
    public void Configure(EntityTypeBuilder<LocationProfile> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(LocationPersistenceConstants.ProfilesTable, LocationModuleMetadata.Schema);

        builder.HasKey(profile => profile.Id);

        builder.Property(profile => profile.Id)
            .HasConversion(LocationValueConverters.Id<LocationProfileId>());

        builder.Property(profile => profile.UserId)
            .HasConversion(LocationValueConverters.Id<UserId>())
            .IsRequired();

        builder.OwnsOne(profile => profile.CurrentLocation, snapshot =>
            LocationSnapshotBuilder.Configure(snapshot, "current_"));

        builder.OwnsOne(profile => profile.TravelLocation, snapshot =>
            LocationSnapshotBuilder.Configure(snapshot, "travel_"));

        builder.Property(profile => profile.TravelMode).IsRequired();

        builder.Property(profile => profile.OriginCountry)
            .HasConversion(LocationValueConverters.CountryCode)
            .HasMaxLength(LocationPersistenceConstants.CountryCodeLength);

        builder.Property(profile => profile.HomeCountry)
            .HasConversion(LocationValueConverters.CountryCode)
            .HasMaxLength(LocationPersistenceConstants.CountryCodeLength);

        builder.Property(profile => profile.PermissionStatus)
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();

        ConfigurePreferences(builder);
        ConfigureSavedLocations(builder);
        ConfigureRecentLocations(builder);

        builder.Property(profile => profile.CreatedAt).IsRequired();

        // One profile per member. The unique index is the invariant made physical: two
        // profiles would mean two current locations, and which one discovery used would
        // depend on row order.
        builder.HasIndex(profile => profile.UserId)
            .IsUnique()
            .HasDatabaseName("ix_location_profiles_user");

        builder.Ignore(profile => profile.DomainEvents);
        builder.Ignore(profile => profile.ActiveLocation);
        builder.Ignore(profile => profile.RecentLocationsByRecency);
    }

    private static void ConfigurePreferences(EntityTypeBuilder<LocationProfile> builder)
    {
        builder.OwnsOne(profile => profile.Preferences, preference =>
        {
            preference.Property(value => value.DistanceUnit)
                .HasConversion<string>()
                .HasColumnName("distance_unit")
                .HasMaxLength(16)
                .IsRequired();

            preference.Property(value => value.CoordinatePrecision)
                .HasConversion<string>()
                .HasColumnName("coordinate_precision")
                .HasMaxLength(16)
                .IsRequired();

            preference.Property(value => value.PreferredCountry)
                .HasConversion(LocationValueConverters.CountryCode)
                .HasColumnName("preferred_country_code")
                .HasMaxLength(LocationPersistenceConstants.CountryCodeLength);

            preference.Property(value => value.PreferredAreaId)
                .HasConversion(LocationValueConverters.Id<AreaId>())
                .HasColumnName("preferred_area_id");

            // Stored as scope plus kilometres rather than as the client's single signed
            // number. The sentinel encoding is a wire concern; a column holding -1 as a
            // distance would be a trap for every future query that reads it.
            preference.OwnsOne(value => value.SearchRadius, radius =>
            {
                radius.Property(value => value.Scope)
                    .HasConversion<string>()
                    .HasColumnName("search_radius_scope")
                    .HasMaxLength(16)
                    .IsRequired();

                radius.OwnsOne(value => value.Distance, distance =>
                    distance.Property(value => value.Kilometers)
                        .HasColumnName("search_radius_km"));

                radius.Navigation(value => value.Distance).IsRequired();
            });

            preference.Navigation(value => value.SearchRadius).IsRequired();
        });

        builder.Navigation(profile => profile.Preferences).IsRequired();
    }

    private static void ConfigureSavedLocations(EntityTypeBuilder<LocationProfile> builder)
    {
        builder.OwnsMany(profile => profile.SavedLocations, saved =>
        {
            saved.ToTable(
                LocationPersistenceConstants.SavedLocationsTable, LocationModuleMetadata.Schema);

            saved.HasKey(location => location.Id);

            saved.Property(location => location.Id)
                .HasConversion(LocationValueConverters.Id<SavedLocationId>());

            saved.Property(location => location.ProfileId)
                .HasConversion(LocationValueConverters.Id<LocationProfileId>())
                .IsRequired();

            saved.WithOwner().HasForeignKey(location => location.ProfileId);

            saved.Property(location => location.Label)
                .HasMaxLength(LocationPersistenceConstants.LabelLength)
                .IsRequired();

            saved.Property(location => location.SavedAt).IsRequired();

            saved.OwnsOne(location => location.Location, snapshot =>
                LocationSnapshotBuilder.Configure(snapshot, string.Empty));

            saved.Navigation(location => location.Location).IsRequired();

            saved.HasIndex(location => new { location.ProfileId, location.Label })
                .IsUnique()
                .HasDatabaseName("ix_saved_locations_profile_label");

            saved.Ignore(location => location.DomainEvents);
        });

        builder.Metadata
            .FindNavigation(nameof(LocationProfile.SavedLocations))?
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }

    private static void ConfigureRecentLocations(EntityTypeBuilder<LocationProfile> builder)
    {
        builder.OwnsMany(profile => profile.RecentLocations, recent =>
        {
            recent.ToTable(
                LocationPersistenceConstants.RecentLocationsTable, LocationModuleMetadata.Schema);

            recent.HasKey(location => location.Id);

            recent.Property(location => location.Id)
                .HasConversion(LocationValueConverters.Id<RecentLocationId>());

            recent.Property(location => location.ProfileId)
                .HasConversion(LocationValueConverters.Id<LocationProfileId>())
                .IsRequired();

            recent.WithOwner().HasForeignKey(location => location.ProfileId);

            recent.Property(location => location.VisitedAt).IsRequired();

            recent.OwnsOne(location => location.Location, snapshot =>
                LocationSnapshotBuilder.Configure(snapshot, string.Empty));

            recent.Navigation(location => location.Location).IsRequired();

            recent.HasIndex(location => new { location.ProfileId, location.VisitedAt })
                .HasDatabaseName("ix_recent_locations_profile_visited");

            recent.Ignore(location => location.DomainEvents);
        });

        builder.Metadata
            .FindNavigation(nameof(LocationProfile.RecentLocations))?
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}
