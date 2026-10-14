using Diaspora.Modules.Location.Integration;
using Diaspora.Modules.Providers.Domain.ValueObjects;

namespace Diaspora.Modules.Providers.Application.Services;

/// <summary>
/// Where a provider is, from what its owner said and what the catalogue resolved.
/// <para>
/// <b>14 Oct 2026.</b> "Zoe hair saloon" was registered in Grand Baie, with the town typed, the
/// street chosen and the pin on Grand Baie, and was stored and shown as <b>Rose Hill</b>, 28 km
/// away. The scope resolver snaps a point to the nearest catalogued town within a wide search
/// radius (right for "what is near me", wrong for "where is this business"), Mauritius's
/// catalogue has no Grand Baie, and registration stored the resolver's town in place of the
/// owner's. Two rules now:
/// </para>
/// <list type="bullet">
/// <item><b>The town shown is the owner's.</b> An inferred place may file a listing for search,
/// never replace what the owner wrote.</item>
/// <item><b>With a pin, the resolver's snap is not trusted for filing.</b> The area is left for
/// <see cref="ProviderAreaFiling"/>, whose point ladder is bounded (a town within 25 km, inside
/// any outline it has), and which files nothing rather than something wrong. Without a pin, the
/// resolver's name match is strict and stands.</item>
/// </list>
/// </summary>
public static class ProviderPlacement
{
    public static ProviderLocationContext FromScope(
        GeographicScope scope,
        string? typedCity,
        double? latitude,
        double? longitude,
        string? addressLine,
        string? postalCode)
    {
        ArgumentNullException.ThrowIfNull(scope);

        var hasPin = latitude is not null && longitude is not null;
        var city = string.IsNullOrWhiteSpace(typedCity) ? scope.CityName : typedCity.Trim();
        var resolverAgrees = !hasPin
            || (scope.CityName is not null && string.Equals(scope.CityName, city, StringComparison.OrdinalIgnoreCase));

        return ProviderLocationContext.Create(
            scope.CountryCode?.Value,
            resolverAgrees ? scope.AreaId?.Value : null,
            areaName: null,
            city,
            latitude ?? scope.Center?.Latitude,
            longitude ?? scope.Center?.Longitude,
            resolverAgrees ? scope.Path?.Value : null,
            addressLine,
            postalCode);
    }
}
