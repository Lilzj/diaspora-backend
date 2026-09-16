using Diaspora.Modules.Location.Integration;
using Diaspora.Modules.Providers.Domain.ValueObjects;

namespace Diaspora.Modules.Providers.Application.Services;

/// <summary>
/// Files a provider under the catalogue node it is in.
/// <para>
/// Registration stores whatever path the search resolver gave the provider's scope, and a scope
/// that resolved no town gave none — leaving the provider uncounted by the providers counter, which
/// aggregates by path, and invisible to the map's cluster endpoint. A provider carries coordinates,
/// so Location's <see cref="IAreaLocator"/> can place it from those first and from the town it named
/// second.
/// </para>
/// </summary>
public sealed class ProviderAreaFiling
{
    private readonly IAreaLocator _locator;

    public ProviderAreaFiling(IAreaLocator locator) => _locator = locator;

    /// <summary>The place, filed. One that is already filed is returned as it is.</summary>
    /// <param name="location">The provider's place.</param>
    /// <param name="namedCity">
    /// The town the provider typed, when the stored place has no city because the scope resolver
    /// did not recognise it. Exactly the case this exists for.
    /// </param>
    /// <param name="cancellationToken">Cancels the lookup.</param>
    public async Task<ProviderLocationContext> FileAsync(
        ProviderLocationContext location,
        string? namedCity = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(location);

        if (!string.IsNullOrWhiteSpace(location.HierarchyPath))
        {
            return location;
        }

        var placement = await _locator.LocateAsync(
            new AreaFilingRequest
            {
                CountryCode = location.CountryCode,
                AreaId = location.AreaId,
                Latitude = location.Latitude,
                Longitude = location.Longitude,
                NeighbourhoodName = location.AreaName,
                CityName = location.CityName ?? namedCity
            },
            cancellationToken);

        return placement is null
            ? location
            : location.WithArea(placement.AreaId, placement.Path);
    }
}
