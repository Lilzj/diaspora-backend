using Diaspora.Modules.Location.Domain.ValueObjects;
using Diaspora.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.Location.Persistence.Configurations;

/// <summary>
/// Maps a <see cref="LocationSnapshot"/> wherever one is owned.
/// <para>
/// Extracted because the snapshot is owned by three different tables — the profile's current
/// and travel locations, saved locations, and recent locations — and three hand-written
/// copies of an eleven-column mapping would eventually differ in a column name or a length.
/// The mapping belongs to the value object; this is where it lives.
/// </para>
/// </summary>
public static class LocationSnapshotBuilder
{
    public static void Configure<TOwner>(
        OwnedNavigationBuilder<TOwner, LocationSnapshot> builder, string prefix)
        where TOwner : class
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Property(snapshot => snapshot.CountryCode)
            .HasConversion(LocationValueConverters.CountryCode)
            .HasColumnName($"{prefix}country_code")
            .HasMaxLength(LocationPersistenceConstants.CountryCodeLength)
            .IsRequired();

        builder.Property(snapshot => snapshot.CountryName)
            .HasColumnName($"{prefix}country_name")
            .HasMaxLength(LocationPersistenceConstants.NameLength)
            .IsRequired();

        builder.Property(snapshot => snapshot.StateName)
            .HasColumnName($"{prefix}state_name")
            .HasMaxLength(LocationPersistenceConstants.NameLength);

        builder.Property(snapshot => snapshot.CityName)
            .HasColumnName($"{prefix}city_name")
            .HasMaxLength(LocationPersistenceConstants.NameLength);

        builder.Property(snapshot => snapshot.Neighborhood)
            .HasColumnName($"{prefix}neighborhood")
            .HasMaxLength(LocationPersistenceConstants.NameLength);

        builder.Property(snapshot => snapshot.Street)
            .HasColumnName($"{prefix}street")
            .HasMaxLength(LocationPersistenceConstants.NameLength);

        builder.Property(snapshot => snapshot.AreaId)
            .HasConversion(LocationValueConverters.Id<AreaId>())
            .HasColumnName($"{prefix}area_id");

        builder.Property(snapshot => snapshot.DisplayName)
            .HasColumnName($"{prefix}display_name")
            .HasMaxLength(LocationPersistenceConstants.NameLength)
            .IsRequired();

        builder.OwnsOne(snapshot => snapshot.Coordinate, coordinate =>
        {
            coordinate.Property(value => value.Latitude).HasColumnName($"{prefix}latitude");
            coordinate.Property(value => value.Longitude).HasColumnName($"{prefix}longitude");
        });
    }
}
