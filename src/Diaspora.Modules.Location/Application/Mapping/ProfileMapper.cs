using Diaspora.Modules.Location.Contracts.V1.Responses;
using Diaspora.Modules.Location.Domain.Profiles;

namespace Diaspora.Modules.Location.Application.Mapping;

/// <summary>
/// Maps the member's geographic context to the wire.
/// <para>
/// The precision setting is applied here and only here, on every coordinate that leaves the
/// process. Enforcing a privacy rule at the mapping boundary rather than at each call site is
/// what makes it impossible to forget: there is no path from a
/// <see cref="Domain.ValueObjects.LocationSnapshot"/> to a response that does not pass
/// through this file.
/// </para>
/// </summary>
public static class ProfileMapper
{
    public static LocationProfileResponse ToResponse(LocationProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var precision = profile.Preferences.CoordinatePrecision;

        return new LocationProfileResponse
        {
            CurrentLocation = profile.CurrentLocation is null
                ? null
                : LocationMapper.ToReference(profile.CurrentLocation, precision),
            TravelLocation = profile.TravelLocation is null
                ? null
                : LocationMapper.ToReference(profile.TravelLocation, precision),
            ActiveLocation = profile.ActiveLocation is null
                ? null
                : LocationMapper.ToReference(profile.ActiveLocation, precision),
            TravelMode = profile.TravelMode,

            // Lower-cased to match the client's own union. Intentional deviation #10.
            PermissionStatus = profile.PermissionStatus.ToString().ToLowerInvariant(),
            OriginCountryCode = profile.OriginCountry?.Value,
            HomeCountryCode = profile.HomeCountry?.Value,
            Preferences = ToResponse(profile.Preferences),
            SavedLocations = profile.SavedLocations
                .OrderBy(saved => saved.Label, StringComparer.OrdinalIgnoreCase)
                .Select(saved => ToResponse(saved, precision))
                .ToList(),
            RecentLocations = profile.RecentLocationsByRecency
                .Select(recent => LocationMapper.ToReference(recent.Location, precision))
                .ToList()
        };
    }

    public static SavedLocationResponse ToResponse(
        SavedLocation saved, Domain.Enums.CoordinatePrecision precision)
    {
        ArgumentNullException.ThrowIfNull(saved);

        return new SavedLocationResponse
        {
            Id = saved.Id.Value,
            Label = saved.Label,
            Location = LocationMapper.ToReference(saved.Location, precision),
            SavedAt = saved.SavedAt
        };
    }

    public static GeographicPreferenceResponse ToResponse(GeographicPreference preferences)
    {
        ArgumentNullException.ThrowIfNull(preferences);

        return new GeographicPreferenceResponse
        {
            // Emitted in the client's own encoding — sentinels included — so RadiusSlider
            // binds without a translation step it would otherwise have to write.
            SearchRadius = preferences.SearchRadius.ToClientValue(),
            RadiusScope = preferences.SearchRadius.Scope.ToString(),
            DistanceUnit = preferences.DistanceUnit.ToString(),
            CoordinatePrecision = preferences.CoordinatePrecision.ToString(),
            PreferredCountryCode = preferences.PreferredCountry?.Value,
            PreferredAreaId = preferences.PreferredAreaId?.Value
        };
    }
}
