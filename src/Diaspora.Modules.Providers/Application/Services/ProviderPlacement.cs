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
/// <item><b>No catalogue node is written</b> (14 Oct 2026, Phase 1 of retiring the place
/// catalogue). A provider is stored at the owner's pin, country and typed town, and is found by
/// them; its area id and path are left empty.</item>
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

        var city = string.IsNullOrWhiteSpace(typedCity) ? scope.CityName : typedCity.Trim();

        // Only the owner's own pin. A scope with no pin may still have a centre — the member's
        // saved location — and a business is not at its owner's home.
        var hasPin = latitude is not null && longitude is not null;

        return ProviderLocationContext.Create(
            scope.CountryCode?.Value,
            areaId: null,
            areaName: null,
            city,
            hasPin ? latitude : null,
            hasPin ? longitude : null,
            hierarchyPath: null,
            addressLine,
            postalCode);
    }
}
