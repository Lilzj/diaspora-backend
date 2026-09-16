using Diaspora.Modules.Location.Integration;
using Diaspora.Modules.Marketplace.Domain.ValueObjects;

namespace Diaspora.Modules.Marketplace.Application.Services;

/// <summary>
/// Files a listing under the catalogue node its place names.
/// <para>
/// <b>A listing is filed by name because it has nothing else — deliberately.</b> A second-hand item
/// is at somebody's home, so this module stores no coordinate (see
/// <see cref="MarketplaceGeographicScopeAdapter"/>). "Rose Hill Central, Rose Hill, MU" is what
/// there is, and Location's <see cref="IAreaLocator"/> turns it into a path the map's cluster counts
/// and every town's listing count can aggregate.
/// </para>
/// <para>
/// The create handler asked the search resolver for a path already; this is what answers when that
/// found nothing — a listing naming only its neighbourhood, or a town typed in a case the resolver
/// did not match — and what files a listing written before either existed.
/// </para>
/// </summary>
public sealed class ListingAreaFiling
{
    private readonly IAreaLocator _locator;

    public ListingAreaFiling(IAreaLocator locator) => _locator = locator;

    /// <summary>The place, filed. One that is already filed is returned as it is.</summary>
    public async Task<ListingLocationContext> FileAsync(
        ListingLocationContext location, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(location);

        if (!string.IsNullOrWhiteSpace(location.AreaPath))
        {
            return location;
        }

        var placement = await _locator.LocateAsync(
            new AreaFilingRequest
            {
                CountryCode = location.CountryCode,
                AreaId = location.AreaId,
                NeighbourhoodName = location.Neighbourhood,
                CityName = location.CityName,
                RegionName = location.RegionName
            },
            cancellationToken);

        return placement is null
            ? location
            : location.WithArea(placement.AreaId, placement.Path);
    }
}
