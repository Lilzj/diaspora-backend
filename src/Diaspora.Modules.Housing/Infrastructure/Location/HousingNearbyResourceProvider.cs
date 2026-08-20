using System.Globalization;
using Diaspora.Modules.Housing.Domain.Enums;
using Diaspora.Modules.Housing.Domain.Listings;
using Diaspora.Modules.Housing.Domain.Policies;
using Diaspora.Modules.Location.Extensibility;

namespace Diaspora.Modules.Housing.Infrastructure.Location;

/// <summary>
/// Contributes properties to Location's nearby endpoint.
/// <para>
/// <b>B4's own summary of this seam names the housing detail screen as the reason it exists</b> —
/// the frontend was making four separate calls and assembling a nearby-ecosystem panel client-side
/// with a fallback chain for the ones that failed. This is the other half of that: Housing both
/// contributes to the endpoint and reads it, so a property page can show nearby jobs and a job page
/// can show nearby housing without either module referencing the other.
/// </para>
/// <para>
/// <b>Distance is reported as zero and that is deliberate.</b> A property does have a coordinate,
/// unlike a marketplace item — but publishing a precise distance from an arbitrary point is a
/// derived way of publishing the address, and the whole disclosure design refuses that. The
/// coordinate a member is entitled to see is fuzzed by <c>PropertyAddress.Disclose</c> on the
/// listing itself; ordering here is by availability and freshness instead.
/// </para>
/// </summary>
public sealed class HousingNearbyResourceProvider : INearbyResourceProvider
{
    /// <summary>One of the resource-type keys Location froze. Not a new one.</summary>
    public const string Key = "housing";

    private readonly IHousingListingRepository _listings;

    public HousingNearbyResourceProvider(IHousingListingRepository listings) =>
        _listings = listings;

    public string ResourceType => Key;

    public async Task<NearbyResourceGroup> FindNearbyAsync(
        NearbyResourceRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var filter = new HousingListingQueryFilter
        {
            CountryCode = request.CountryCode.Value,
            Statuses =
            [
                HousingListingStatus.Available,
                HousingListingStatus.Published
            ],
            AvailableOnly = true,

            // Nearby is a public surface with no viewer audience of its own, so no viewer is
            // supplied and no draft, unlisted or hidden property can reach it. B11 must not assume
            // a caller's audience on Location's behalf.
            ViewerId = null,
            ViewerIsStaff = false,
            Sort = HousingSortOrder.Newest,
            Limit = Math.Clamp(request.Limit, 1, HousingLimits.MaxPageSize)
        };

        var page = await _listings.QueryAsync(
            filter, cursor: null, filter.EffectiveLimit, cancellationToken);

        var items = page.Items
            .OrderByDescending(listing => listing.Availability.HasUnitsRemaining)
            .ThenByDescending(listing => listing.PublishedAt)
            .Select(listing => new NearbyResourceItem
            {
                Id = listing.Id.Value.ToString(),
                Title = listing.Title.Value,
                Category = listing.AccommodationTypeKey,

                // Not a distance. See the class summary.
                DistanceKm = 0d,
                CityName = listing.Location.CityName,
                AreaName = listing.Location.NeighborhoodName ?? listing.Location.RegionName,
                Payload = BuildPayload(listing)
            })
            .ToList();

        return new NearbyResourceGroup { ResourceType = ResourceType, Items = items };
    }

    /// <summary>
    /// What a nearby card renders. Strings only, because the contract is a string map and because a
    /// consumer that wants the property itself has its identifier.
    /// </summary>
    private static Dictionary<string, string> BuildPayload(HousingListing listing)
    {
        var payload = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["slug"] = listing.Slug.Value,
            ["accommodationType"] = listing.AccommodationTypeKey,
            ["bedrooms"] = listing.Capacity.Bedrooms.ToString(CultureInfo.InvariantCulture),
            ["furnished"] = listing.FurnishedStatus.ToString(),
            ["available"] =
                listing.Availability.AvailableUnits.ToString(CultureInfo.InvariantCulture),
            ["verified"] = listing.TrustMark.IsVerified
                ? bool.TrueString
                : bool.FalseString,
            ["shortStay"] = listing.StayPolicy.AcceptsShortStay
                ? bool.TrueString
                : bool.FalseString
        };

        // The rent only where there is one, and always with its currency and period. A bare number
        // on a multi-country surface is a number a member reads in their own currency and at their
        // own billing cycle.
        if (listing.Price is { IsDisclosed: true, Amount: { } amount, CurrencyCode: { } currency })
        {
            payload["rent"] = amount.ToString(CultureInfo.InvariantCulture);
            payload["currency"] = currency;
            payload["period"] = listing.Price.Period.ToString();
        }

        if (listing.Availability.AvailableFrom is { } availableFrom)
        {
            payload["availableFrom"] = availableFrom.ToString("O", CultureInfo.InvariantCulture);
        }

        if (listing.CoverImage is { } cover)
        {
            payload["coverMediaId"] = cover.MediaId.ToString();
        }

        return payload;
    }
}
