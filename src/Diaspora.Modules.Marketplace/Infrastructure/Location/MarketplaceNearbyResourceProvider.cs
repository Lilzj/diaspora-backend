using System.Globalization;
using Diaspora.Modules.Location.Extensibility;
using Diaspora.Modules.Marketplace.Domain.Enums;
using Diaspora.Modules.Marketplace.Domain.Listings;
using Diaspora.Modules.Marketplace.Domain.Policies;

namespace Diaspora.Modules.Marketplace.Infrastructure.Location;

/// <summary>
/// Contributes listings to Location's <c>GET /nearby</c>.
/// <para>
/// The resource type is <c>marketplace</c> — one of the keys B4's contract freeze published, and one
/// the frontend already calls separately before assembling its "nearby ecosystem" panel client-side.
/// B10 claims a reserved key rather than inventing one, and adds nothing whatsoever to Location.
/// </para>
/// <para>
/// <b>A listing is area-scoped, not point-scoped, so every item's distance is zero.</b> That is not
/// an omission: a second-hand item is at somebody's home, and this module deliberately stores no
/// coordinate for it — publishing one would publish where a member lives to everybody who opens the
/// page. Ordering is therefore by freshness and trust rather than by geometry, and a computed
/// distance from an area centroid would be a number the client renders as a fact about a place the
/// platform does not actually know.
/// </para>
/// </summary>
public sealed class MarketplaceNearbyResourceProvider : INearbyResourceProvider
{
    /// <summary>One of the resource-type keys Location froze. Not a new one.</summary>
    public const string Key = "marketplace";

    private readonly IMarketplaceListingRepository _listings;

    public MarketplaceNearbyResourceProvider(IMarketplaceListingRepository listings) =>
        _listings = listings;

    public string ResourceType => Key;

    public async Task<NearbyResourceGroup> FindNearbyAsync(
        NearbyResourceRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // No catalogue node (14 Oct 2026). A circle is the rectangle around it over the pins
        // sellers chose to give, with unpinned listings kept when they name the requested town; a
        // town- or country-wide search is the town's name and the country.
        var circle = request.IsCircle ? request.Search.Bounds : null;

        var filter = new ListingQueryFilter
        {
            CountryCode = request.CountryCode?.Value,
            CityName = circle is null ? request.CityName : null,
            UnpinnedCityName = circle is null ? null : request.CityName,
            MinLatitude = circle?.South,
            MaxLatitude = circle?.North,
            MinLongitude = circle?.West,
            MaxLongitude = circle?.East,
            Statuses = [ListingStatus.Active],
            AvailableOnly = true,

            // Nearby is a public surface with no viewer audience of its own, so no viewer is
            // supplied and no draft, unlisted or hidden listing can reach it. B10 must not assume a
            // caller's audience on Location's behalf.
            ViewerId = null,
            ViewerIsStaff = false,
            Sort = ListingSortOrder.Newest,
            Limit = Math.Clamp(request.Limit, 1, MarketplaceLimits.MaxPageSize)
        };

        var page = await _listings.QueryAsync(
            filter, cursor: null, filter.EffectiveLimit, cancellationToken);

        var items = page.Items
            .OrderByDescending(listing => listing.IsFeatured)
            .ThenByDescending(listing => listing.PublishedAt)
            .Select(listing => new NearbyResourceItem
            {
                Id = listing.Id.Value.ToString(),
                Title = listing.Title.Value,
                Category = listing.CategoryKey,

                // Goods have an area, not a coordinate. See the class summary.
                DistanceKm = 0d,
                CityName = listing.Location.CityName,
                AreaName = listing.Location.Neighbourhood ?? listing.Location.RegionName,
                Payload = BuildPayload(listing)
            })
            .ToList();

        return new NearbyResourceGroup { ResourceType = ResourceType, Items = items };
    }

    /// <summary>
    /// What a nearby card renders. Strings only, because the contract is a string map and because
    /// a consumer that wants the listing itself has its identifier.
    /// </summary>
    private static Dictionary<string, string> BuildPayload(MarketplaceListing listing)
    {
        var payload = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["slug"] = listing.Slug.Value,
            ["listingType"] = listing.ListingType.ToString(),
            ["condition"] = listing.Condition.ToString(),
            ["isFeatured"] = listing.IsFeatured ? bool.TrueString : bool.FalseString,
            ["isFree"] = listing.Price.IsFree ? bool.TrueString : bool.FalseString,
            ["offersDelivery"] =
                listing.Fulfillment.OffersDelivery ? bool.TrueString : bool.FalseString,
            ["available"] = listing.Inventory.Available.ToString(CultureInfo.InvariantCulture)
        };

        // The price only where there is one, and always with its currency. A bare number on a
        // multi-country surface is a number a member will read in their own currency.
        if (listing.Price is { IsDisclosed: true, Amount: { } amount, CurrencyCode: { } currency })
        {
            payload["price"] = amount.ToString(CultureInfo.InvariantCulture);
            payload["currency"] = currency;
        }

        if (listing.CoverImage is { } cover)
        {
            payload["coverMediaId"] = cover.MediaId.ToString();
        }

        return payload;
    }
}
