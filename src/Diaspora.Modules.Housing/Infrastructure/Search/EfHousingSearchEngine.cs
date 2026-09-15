using Diaspora.Modules.Housing.Application.Services;
using Diaspora.Modules.Housing.Domain.Catalog;
using Diaspora.Modules.Housing.Domain.Enums;
using Diaspora.Modules.Housing.Domain.Listings;
using Diaspora.Modules.Housing.Extensibility.Search;
using Diaspora.Modules.Housing.Persistence;

namespace Diaspora.Modules.Housing.Infrastructure.Search;

/// <summary>
/// The shipped search engine, answering from the write model.
/// <para>
/// <b>Honest and adequate, and deliberately not clever.</b> It has no relevance score and no
/// geometry: a text search is a <c>LIKE</c> over titles, summaries and place names, and "nearest
/// first" measures the homes inside a radius search's circle in memory, through Location, and
/// degrades to newest — saying so — when there is no circle or too much in it. That is what a
/// relational database can do well, and pretending otherwise would mean building a poor search
/// engine inside a module that has no business containing one.
/// </para>
/// <para>
/// <b>What it does do properly is everything structured.</b> Bedrooms, rent bands, amenities,
/// availability dates, neighbourhood, verification — all of it is indexed and exact, and those are
/// the filters a member actually narrows a housing search with. A real engine replaces this for
/// text and geometry and changes nothing above <c>IHousingSearchEngine</c>.
/// </para>
/// <para>
/// <b>Facets are computed by re-running the filter with each axis relaxed.</b> Expensive, and
/// bounded by only ever being done when the caller asked for facets — a nearby panel does not, and
/// a search screen does it once per page. An external engine computes them in the same round trip
/// and this cost disappears with it.
/// </para>
/// </summary>
public sealed class EfHousingSearchEngine : IHousingSearchEngine
{
    /// <summary>
    /// How many listings a facet pass may read. Facets describe a result set well enough from its
    /// first few hundred rows, and reading a whole country to count checkboxes would make the
    /// search slower than the search.
    /// </summary>
    private const int FacetSampleSize = 500;

    /// <summary>
    /// How many homes a "nearest first" search will measure. Every one inside the circle is
    /// measured and sorted before a page is cut, which is what lets page two continue page one;
    /// past this many the search says so and answers newest first rather than sorting a sample.
    /// </summary>
    internal const int NearestCandidateLimit = 1_000;

    private readonly IHousingListingRepository _listings;
    private readonly IHousingAmenityRepository _amenities;
    private readonly HousingGeographicScopeAdapter _geography;

    public EfHousingSearchEngine(
        IHousingListingRepository listings,
        IHousingAmenityRepository amenities,
        HousingGeographicScopeAdapter geography)
    {
        _listings = listings;
        _amenities = amenities;
        _geography = geography;
    }

    public string Name => "ef";

    public async Task<HousingSearchOutcome> SearchAsync(
        HousingSearchRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        HousingSortOrder? appliedSort = null;

        if (request.Filter.Sort == HousingSortOrder.Nearest)
        {
            if (await NearestAsync(request, cancellationToken) is { } nearest)
            {
                return nearest;
            }

            // No point to measure from, or too many homes to measure: newest first, and said so.
            appliedSort = HousingSortOrder.Newest;
            request = request with { Filter = request.Filter with { Sort = HousingSortOrder.Newest } };
        }

        var page = await _listings.QueryAsync(
            request.Filter,
            request.Cursor,
            request.Filter.EffectiveLimit,
            cancellationToken);

        return new HousingSearchOutcome
        {
            Items = page.Items,
            NextCursor = page.NextCursor,
            HasMore = page.HasMore,
            TotalCount = await _listings.CountAsync(request.Filter, cancellationToken),
            Facets = request.IncludeFacets
                ? await ComputeFacetsAsync(request.Filter, cancellationToken)
                : null,
            EngineName = Name,
            AppliedSort = appliedSort
        };
    }

    /// <summary>
    /// "Nearest first", across every page.
    /// <para>
    /// <b>Measured in memory, never in SQL.</b> A distance is Location's to compute — the rule every
    /// module keeps, and a haversine in a query would be a second, subtly different one — and it
    /// is measured from the coordinate a stranger is shown
    /// (<see cref="HousingGeographicScopeAdapter.KilometresToPublishedPosition"/>), so the order
    /// reveals nothing the distance on each card does not already. Before this the page was sorted on the phone, twenty homes at
    /// a time, and a closer home on page two stayed below page one.
    /// </para>
    /// <para>
    /// Every candidate inside the circle is read (identifiers and coordinates only), measured and
    /// ordered by distance then slug, and the page is cut from that order after the cursor. Null
    /// when there is no point or radius, or when the circle holds more than
    /// <see cref="NearestCandidateLimit"/>.
    /// </para>
    /// </summary>
    private async Task<HousingSearchOutcome?> NearestAsync(
        HousingSearchRequest request, CancellationToken cancellationToken)
    {
        if (request is not { Latitude: { } latitude, Longitude: { } longitude, RadiusKm: { } radiusKm })
        {
            return null;
        }

        var candidates = await _listings.QueryNearestCandidatesAsync(
            request.Filter, NearestCandidateLimit + 1, cancellationToken);

        if (candidates.Count > NearestCandidateLimit)
        {
            return null;
        }

        // The same edge the handler's radius check applies: distances are shown to a tenth, so the
        // boundary is given the same tenth back.
        var edge = radiusKm + 0.05d;

        var ordered = candidates
            .Select(candidate => (
                Candidate: candidate,
                Kilometres: _geography.KilometresToPublishedPosition(
                    latitude, longitude, candidate.Latitude, candidate.Longitude, candidate.Visibility)))
            .Where(measured => measured.Kilometres is { } km && Math.Round(km, 1) <= edge)
            .OrderBy(measured => measured.Kilometres)
            .ThenBy(measured => measured.Candidate.Slug, StringComparer.Ordinal)
            .ToList();

        var after = HousingCursor.Parse(request.Cursor);

        var remaining = after is { } cursor && cursor.AsNumber() is { } afterKm
            ? ordered.SkipWhile(measured =>
                measured.Kilometres < afterKm
                || (measured.Kilometres == afterKm
                    && string.CompareOrdinal(measured.Candidate.Slug, cursor.LastKey) <= 0))
            : ordered;

        var limit = request.Filter.EffectiveLimit;
        var window = remaining.Take(limit + 1).ToList();
        var hasMore = window.Count > limit;
        var pageRows = window.Take(limit).ToList();

        var cards = await _listings.GetManyAsync(
            [.. pageRows.Select(measured => measured.Candidate.Id)], cancellationToken);

        var byId = cards.ToDictionary(listing => listing.Id);

        var items = pageRows
            .Select(measured => byId.GetValueOrDefault(measured.Candidate.Id))
            .OfType<HousingListing>()
            .ToList();

        return new HousingSearchOutcome
        {
            Items = items,
            NextCursor = hasMore && pageRows.Count > 0
                ? HousingCursor.FromNumber(pageRows[^1].Kilometres!.Value, pageRows[^1].Candidate.Slug).Encode()
                : null,
            HasMore = hasMore,
            TotalCount = ordered.Count,
            Facets = request.IncludeFacets
                ? await ComputeFacetsAsync(request.Filter, cancellationToken)
                : null,
            EngineName = Name
        };
    }

    /// <summary>
    /// Counts the axes a search screen renders beside its filters.
    /// <para>
    /// Computed from a bounded sample of the matching set rather than from the whole of it. A count
    /// that is approximate on a large result set is exactly as useful to a member deciding which box
    /// to tick, and a count that is exact but slow is one the screen renders after they have
    /// already ticked something.
    /// </para>
    /// </summary>
    private async Task<HousingSearchFacets> ComputeFacetsAsync(
        HousingListingQueryFilter filter, CancellationToken cancellationToken)
    {
        var sample = await _listings.QueryAsync(
            filter with { Limit = FacetSampleSize }, cursor: null, FacetSampleSize,
            cancellationToken);

        var listings = sample.Items;

        if (listings.Count == 0)
        {
            return HousingSearchFacets.Empty;
        }

        var accessibleKeys = (await _amenities.GetAllAsync(
                includeInactive: false, cancellationToken))
            .Where(amenity => amenity.IsAccessibilityFeature)
            .Select(amenity => amenity.Key)
            .ToHashSet(StringComparer.Ordinal);

        return new HousingSearchFacets
        {
            AccommodationTypes = listings
                .GroupBy(listing => listing.AccommodationTypeKey, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Count(),
                    StringComparer.Ordinal),

            Amenities = listings
                .SelectMany(listing => listing.Amenities)
                .GroupBy(amenity => amenity.AmenityKey, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Count(),
                    StringComparer.Ordinal),

            Bedrooms = listings
                .GroupBy(listing => listing.Capacity.Bedrooms)
                .ToDictionary(group => group.Key, group => group.Count()),

            Neighborhoods = listings
                .Where(listing => listing.Location.NeighborhoodAreaId is not null)
                .GroupBy(listing => listing.Location.NeighborhoodAreaId!.Value)
                .ToDictionary(group => group.Key, group => group.Count()),

            LeaseTypes = listings
                .GroupBy(listing => listing.LeaseType.ToString())
                .ToDictionary(group => group.Key, group => group.Count(),
                    StringComparer.Ordinal),

            FurnishedStatuses = listings
                .GroupBy(listing => listing.FurnishedStatus.ToString())
                .ToDictionary(group => group.Key, group => group.Count(),
                    StringComparer.Ordinal),

            PriceRanges = ComputePriceRanges(listings),

            VerifiedCount = listings.Count(listing => listing.TrustMark.IsVerified),

            AccessibleCount = listings.Count(listing =>
                listing.Amenities.Any(amenity =>
                    accessibleKeys.Contains(amenity.AmenityKey))),

            ShortStayCount = listings.Count(listing => listing.StayPolicy.AcceptsShortStay),

            RoommateWantedCount = listings.Count(
                listing => listing.StayPolicy.IsLookingForRoommate)
        };
    }

    /// <summary>
    /// One rent range per currency and period pair present in the sample.
    /// <para>
    /// Grouped rather than aggregated, because there is no single range across a nightly hostel bed
    /// and a monthly flat — and a slider drawn across both is a control nobody can use.
    /// </para>
    /// </summary>
    private static Dictionary<string, HousingPriceRangeFacet> ComputePriceRanges(
        IReadOnlyList<HousingListing> listings) =>
        listings
            .Where(listing => listing.Price is
                { IsDisclosed: true, Amount: > 0m, CurrencyCode: { Length: > 0 } }
                && listing.Price.Period != RentPeriod.None)
            .GroupBy(listing => new
            {
                Currency = listing.Price.CurrencyCode!,
                listing.Price.Period
            })
            .ToDictionary(
                group => HousingPriceRangeFacet.KeyFor(group.Key.Currency, group.Key.Period),
                group => new HousingPriceRangeFacet
                {
                    CurrencyCode = group.Key.Currency,
                    Period = group.Key.Period,
                    Minimum = group.Min(listing => listing.Price.Amount!.Value),
                    Maximum = group.Max(listing => listing.Price.Amount!.Value),
                    Count = group.Count()
                },
                StringComparer.Ordinal);
}
