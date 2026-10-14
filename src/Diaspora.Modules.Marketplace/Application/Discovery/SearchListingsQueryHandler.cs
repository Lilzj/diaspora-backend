using Diaspora.Application.Abstractions;
using Diaspora.Modules.Marketplace.Application.Mapping;
using Diaspora.Modules.Marketplace.Application.Services;
using Diaspora.Modules.Marketplace.Contracts.V1.Responses;
using Diaspora.Modules.Marketplace.Domain.Enums;
using Diaspora.Modules.Marketplace.Domain.Identifiers;
using Diaspora.Modules.Marketplace.Domain.Listings;
using Diaspora.Modules.Marketplace.Domain.Policies;
using Diaspora.Modules.Marketplace.Domain.Saved;
using Diaspora.Modules.Marketplace.Domain.Sellers;
using Diaspora.Modules.Marketplace.Extensibility.Ranking;
using Diaspora.Modules.Marketplace.Observability;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Marketplace.Application.Discovery;

/// <summary>
/// Runs a search or a browse.
/// <para>
/// <b>Geography is resolved through B4 before the filter is built, never after.</b> "Near me", a
/// radius sentinel, a city name and an area identifier all become one normalised scope, and the
/// filter narrows by country and materialised path. This module parses no radius and computes no
/// distance.
/// </para>
/// <para>
/// <b>The ranking is applied after the engine answers, and only reorders the page.</b> It cannot
/// widen a result set, so a strategy cannot leak anything the audience filter excluded — and it
/// cannot bury a listing off the page, only within it.
/// </para>
/// <para>
/// <b>The price band is dropped when it arrives without a currency.</b> Nothing here converts, and
/// a bound applied across four currencies would be wrong in three of them; dropping it returns more
/// than the buyer asked for rather than the wrong things.
/// </para>
/// </summary>
public sealed class SearchListingsQueryHandler
    : IQueryHandler<SearchListingsQuery, ListingSearchResultResponse>
{
    private readonly IMarketplaceListingRepository _listings;
    private readonly ISellerProfileRepository _sellers;
    private readonly ISavedListingRepository _saved;
    private readonly MarketplaceAuthorizationService _authorization;
    private readonly MarketplaceGeographicScopeAdapter _geography;
    private readonly ListingCategoryResolver _categories;
    private readonly ListingRankingStrategyRegistry _rankings;
    private readonly IClock _clock;

    public SearchListingsQueryHandler(
        IMarketplaceListingRepository listings,
        ISellerProfileRepository sellers,
        ISavedListingRepository saved,
        MarketplaceAuthorizationService authorization,
        MarketplaceGeographicScopeAdapter geography,
        ListingCategoryResolver categories,
        ListingRankingStrategyRegistry rankings,
        IClock clock)
    {
        _listings = listings;
        _sellers = sellers;
        _saved = saved;
        _authorization = authorization;
        _geography = geography;
        _categories = categories;
        _rankings = rankings;
        _clock = clock;
    }

    public async Task<Result<ListingSearchResultResponse>> Handle(
        SearchListingsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var request = query.Request;

        MarketplaceTelemetry.SearchesRun.Add(
            1,
            new KeyValuePair<string, object?>(
                "kind", string.IsNullOrWhiteSpace(request.Text) ? "browse" : "search"));

        var filter = await BuildFilterAsync(request, cancellationToken);

        var scope = await ResolveScopeAsync(request, cancellationToken);

        if (scope is not null)
        {
            filter = MarketplaceGeographicScopeAdapter.Apply(filter, scope);
        }

        var page = await _listings.QueryAsync(
            filter, request.Cursor, filter.EffectiveLimit, cancellationToken);

        // A circle's rectangle has corners outside the circle; Location measures them away.
        var inside = scope is null ? page.Items : _geography.WithinCircle(scope, page.Items);

        var sellers = await _sellers.GetManyAsync(
            [.. inside.Select(listing => listing.SellerId).Distinct()], cancellationToken);

        var savedIds = await ResolveSavedAsync(inside, cancellationToken);

        var ordered = ApplyRanking(inside, sellers, request.Ranking);

        var cards = ordered
            .Select(listing => ListingMapper.ToCard(
                listing,
                sellers.GetValueOrDefault(listing.SellerId),
                savedIds?.Contains(listing.Id.Value)))
            .ToList();

        return Result.Success(new ListingSearchResultResponse
        {
            Items = cards,
            NextCursor = page.NextCursor,
            HasMore = page.HasMore,

            // Deliberately absent. Counting the whole matching set is a second query over the same
            // predicate, and "about 400 results" is not worth doubling every search.
            TotalMatches = null,
            Facets = null,
            ResolvedScope = scope is null
                ? null
                : MarketplaceGeographicScopeAdapter.Describe(scope),
            Ranking = request.Ranking
        });
    }

    private async Task<ListingQueryFilter> BuildFilterAsync(
        Contracts.V1.Requests.SearchListingsRequest request,
        CancellationToken cancellationToken)
    {
        var categoryPath = await _categories.ResolvePathPrefixAsync(
            request.CategoryPath, cancellationToken);

        // Dropped without a currency. See the class summary.
        var hasCurrency = !string.IsNullOrWhiteSpace(request.Currency);

        var filter = new ListingQueryFilter
        {
            Text = request.Text,
            CategoryKey = request.CategoryKey,
            CategoryPathPrefix = categoryPath,
            Tag = request.Tag,
            Attributes = ParseAttributes(request.Attributes),
            Brand = request.Brand,
            Model = request.Model,
            MinimumCondition = MarketplaceWireNames.ParseCondition(request.MinimumCondition),
            ListingTypes = ParseMany(request.ListingTypes, MarketplaceWireNames.ParseListingType),
            SellerKinds = ParseMany(request.SellerKinds, MarketplaceWireNames.ParseSellerKind),
            CountryCode = request.CountryCode,
            CityName = request.City,
            MinimumPrice = hasCurrency ? request.MinimumPrice : null,
            MaximumPrice = hasCurrency ? request.MaximumPrice : null,
            Currency = request.Currency,
            FreeOnly = request.FreeOnly,
            NegotiableOnly = request.NegotiableOnly,
            OffersDeliveryOnly = request.DeliveryOnly,
            OffersShippingOnly = request.ShippingOnly,
            PickupOnly = request.PickupOnly,
            SellerId = request.SellerId is { } sellerId
                ? SellerProfileId.From(sellerId)
                : null,
            VerifiedSellersOnly = request.VerifiedSellersOnly,
            FeaturedOnly = request.FeaturedOnly,
            AvailableOnly = true,
            Sort = MarketplaceWireNames.ParseSortOrder(request.Sort) ?? ListingSortOrder.Newest,
            Limit = request.Limit ?? MarketplaceLimits.DefaultPageSize
        };

        return await _authorization.ApplyAudienceAsync(
            filter, includeOwn: false, cancellationToken);
    }

    private async Task<Modules.Location.Integration.GeographicScope?> ResolveScopeAsync(
        Contracts.V1.Requests.SearchListingsRequest request,
        CancellationToken cancellationToken)
    {
        if (request.AreaId is null
            && request.Latitude is null
            && request.RadiusKm is null
            && string.IsNullOrWhiteSpace(request.Bounds)
            && string.IsNullOrWhiteSpace(request.City))
        {
            return null;
        }

        var scope = await _geography.ResolveAsync(
            request.City,
            request.CountryCode,
            request.AreaId,
            request.Latitude,
            request.Longitude,
            request.RadiusKm,
            request.Bounds,
            _authorization.CallerId,
            cancellationToken);

        return scope.IsSuccess ? scope.Value : null;
    }

    private async Task<IReadOnlySet<Guid>?> ResolveSavedAsync(
        IReadOnlyList<MarketplaceListing> listings, CancellationToken cancellationToken)
    {
        if (_authorization.CallerId is not { } viewer || listings.Count == 0)
        {
            return null;
        }

        return await _saved.FindSavedListingIdsAsync(
            viewer, [.. listings.Select(listing => listing.Id)], cancellationToken);
    }

    /// <summary>
    /// Reorders the page with whichever strategy the caller named. Reordering only — the set is
    /// whatever the filter returned, so a strategy can never widen it.
    /// </summary>
    private IReadOnlyList<MarketplaceListing> ApplyRanking(
        IReadOnlyList<MarketplaceListing> listings,
        IReadOnlyDictionary<SellerProfileId, SellerProfile> sellers,
        string? rankingKey)
    {
        if (string.IsNullOrWhiteSpace(rankingKey) || listings.Count < 2)
        {
            return listings;
        }

        var strategy = _rankings.Resolve(rankingKey);

        if (strategy is null)
        {
            return listings;
        }

        var now = _clock.UtcNow;

        return
        [
            .. listings
                .Select(listing => new
                {
                    Listing = listing,
                    Score = strategy.Score(
                        ListingSearchProjectionService.ToRankingContext(
                            listing, sellers.GetValueOrDefault(listing.SellerId), now))
                })
                .OrderByDescending(scored => scored.Score)
                .Select(scored => scored.Listing)
        ];
    }

    /// <summary>
    /// Parses <c>key:value</c> attribute filters. Malformed entries are dropped rather than
    /// refused: a stale client sending one bad pair should get a narrower answer, not an error page.
    /// </summary>
    private static List<KeyValuePair<string, string>>? ParseAttributes(
        IReadOnlyList<string>? raw)
    {
        if (raw is not { Count: > 0 })
        {
            return null;
        }

        var parsed = new List<KeyValuePair<string, string>>();

        foreach (var entry in raw)
        {
            var separator = entry?.IndexOf(':', StringComparison.Ordinal) ?? -1;

            if (separator <= 0 || separator == entry!.Length - 1)
            {
                continue;
            }

            var key = ListingAttribute.NormalizeKey(entry[..separator]);

            if (key is not null)
            {
                parsed.Add(new KeyValuePair<string, string>(
                    key, entry[(separator + 1)..].Trim()));
            }
        }

        return parsed.Count == 0 ? null : parsed;
    }

    private static List<TEnum>? ParseMany<TEnum>(
        IReadOnlyList<string>? raw, Func<string?, TEnum?> parse)
        where TEnum : struct
    {
        if (raw is not { Count: > 0 })
        {
            return null;
        }

        var parsed = raw
            .Select(parse)
            .Where(value => value.HasValue)
            .Select(value => value!.Value)
            .ToList();

        return parsed.Count == 0 ? null : parsed;
    }
}
