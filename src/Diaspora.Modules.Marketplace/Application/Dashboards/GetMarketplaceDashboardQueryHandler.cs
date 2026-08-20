using Diaspora.Application.Abstractions;
using Diaspora.Modules.Marketplace.Application.Mapping;
using Diaspora.Modules.Marketplace.Application.Services;
using Diaspora.Modules.Marketplace.Contracts.V1.Responses;
using Diaspora.Modules.Marketplace.Domain.Catalog;
using Diaspora.Modules.Marketplace.Domain.Enums;
using Diaspora.Modules.Marketplace.Domain.Listings;
using Diaspora.Modules.Marketplace.Domain.Offers;
using Diaspora.Modules.Marketplace.Domain.Reservations;
using Diaspora.Modules.Marketplace.Domain.Saved;
using Diaspora.Modules.Marketplace.Domain.Sellers;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Marketplace.Application.Dashboards;

/// <summary>
/// Assembles the marketplace home surface: the caller's own state where they have any, and three
/// discovery rails where they do not.
/// <para>
/// <b>Everything degrades rather than fails.</b> An anonymous reader gets the rails and zeroes; a
/// member with no seller profile gets their watchlist and their offers; a caller who supplied no
/// geography gets the country rails and no nearby one. There is no combination of inputs that
/// produces an error page, because a home screen that can error is a home screen that sometimes
/// does.
/// </para>
/// </summary>
public sealed class GetMarketplaceDashboardQueryHandler
    : IQueryHandler<GetMarketplaceDashboardQuery, MarketplaceDashboardResponse>
{
    private const int RailSize = 12;

    private readonly IMarketplaceListingRepository _listings;
    private readonly ISellerProfileRepository _sellers;
    private readonly ISavedListingRepository _saved;
    private readonly IListingOfferRepository _offers;
    private readonly IListingReservationRepository _reservations;
    private readonly IListingCategoryRepository _categories;
    private readonly MarketplaceAuthorizationService _authorization;
    private readonly MarketplaceGeographicScopeAdapter _geography;

    public GetMarketplaceDashboardQueryHandler(
        IMarketplaceListingRepository listings,
        ISellerProfileRepository sellers,
        ISavedListingRepository saved,
        IListingOfferRepository offers,
        IListingReservationRepository reservations,
        IListingCategoryRepository categories,
        MarketplaceAuthorizationService authorization,
        MarketplaceGeographicScopeAdapter geography)
    {
        _listings = listings;
        _sellers = sellers;
        _saved = saved;
        _offers = offers;
        _reservations = reservations;
        _categories = categories;
        _authorization = authorization;
        _geography = geography;
    }

    public async Task<Result<MarketplaceDashboardResponse>> Handle(
        GetMarketplaceDashboardQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var seller = await _authorization.FindCallerSellerAsync(cancellationToken);

        var categories = await _categories.GetAllAsync(
            includeInactive: false, cancellationToken);

        var featured = await ReadRailAsync(
            filter => filter with { FeaturedOnly = true, Sort = ListingSortOrder.Featured },
            query.CountryCode,
            cancellationToken);

        var recent = await ReadRailAsync(
            filter => filter with { Sort = ListingSortOrder.Newest },
            query.CountryCode,
            cancellationToken);

        var (nearby, scopeName) = await ReadNearbyAsync(query, cancellationToken);

        var buyerState = await ReadBuyerStateAsync(cancellationToken);

        return Result.Success(new MarketplaceDashboardResponse
        {
            Seller = seller is null
                ? null
                : SellerMapper.ToResponse(seller, canManage: true, _authorization.IsStaff),
            OffersAwaitingYou = buyerState.Offers,
            ActiveReservations = buyerState.Reservations,
            SavedListings = buyerState.Saved,

            // Not computed here. Answering it means comparing each saved listing's current price
            // against what it was when saved, and this module deliberately keeps no price history —
            // a future price-watch consumer of ListingUpdated is where that belongs.
            SavedListingsWithPriceDrops = 0,
            TopCategories =
            [
                .. categories
                    .Where(category => category.Depth == 1)
                    .OrderBy(category => category.Order)
                    .Select(CatalogMapper.ToResponse)
            ],
            Featured = featured,
            RecentlyAdded = recent,
            Nearby = nearby,
            ResolvedScope = scopeName
        });
    }

    private async Task<List<ListingCardResponse>> ReadRailAsync(
        Func<ListingQueryFilter, ListingQueryFilter> narrow,
        string? countryCode,
        CancellationToken cancellationToken)
    {
        var filter = narrow(await _authorization.ApplyAudienceAsync(
            new ListingQueryFilter
            {
                CountryCode = countryCode,
                AvailableOnly = true,
                Limit = RailSize
            },
            includeOwn: false,
            cancellationToken));

        var page = await _listings.QueryAsync(filter, cursor: null, RailSize, cancellationToken);

        return await ToCardsAsync(page.Items, cancellationToken);
    }

    private async Task<(List<ListingCardResponse> Items, string? Scope)> ReadNearbyAsync(
        GetMarketplaceDashboardQuery query, CancellationToken cancellationToken)
    {
        if (query.AreaId is null
            && query.Latitude is null
            && string.IsNullOrWhiteSpace(query.City))
        {
            return ([], null);
        }

        var scope = await _geography.ResolveAsync(
            query.City,
            query.CountryCode,
            query.AreaId,
            query.Latitude,
            query.Longitude,
            query.RadiusKm,
            _authorization.CallerId,
            cancellationToken);

        if (scope.IsFailure)
        {
            return ([], null);
        }

        var filter = MarketplaceGeographicScopeAdapter.Apply(
            await _authorization.ApplyAudienceAsync(
                new ListingQueryFilter
                {
                    AvailableOnly = true,
                    Sort = ListingSortOrder.Newest,
                    Limit = RailSize
                },
                includeOwn: false,
                cancellationToken),
            scope.Value);

        var page = await _listings.QueryAsync(filter, cursor: null, RailSize, cancellationToken);

        return (
            await ToCardsAsync(page.Items, cancellationToken),
            MarketplaceGeographicScopeAdapter.Describe(scope.Value));
    }

    private async Task<(int Offers, int Reservations, int Saved)> ReadBuyerStateAsync(
        CancellationToken cancellationToken)
    {
        if (_authorization.CallerId is not { } viewer)
        {
            return (0, 0, 0);
        }

        var offers = await _offers.CountAsync(
            new OfferQueryFilter
            {
                BuyerUserId = viewer,
                OpenOnly = true,
                AwaitingParty = OfferParty.Buyer
            },
            cancellationToken);

        var reservations = await _reservations.CountActiveByBuyerAsync(viewer, cancellationToken);
        var saved = await _saved.CountForUserAsync(viewer, cancellationToken);

        return (offers, reservations, saved);
    }

    private async Task<List<ListingCardResponse>> ToCardsAsync(
        IReadOnlyList<MarketplaceListing> listings, CancellationToken cancellationToken)
    {
        if (listings.Count == 0)
        {
            return [];
        }

        var sellers = await _sellers.GetManyAsync(
            [.. listings.Select(listing => listing.SellerId).Distinct()], cancellationToken);

        var saved = _authorization.CallerId is { } viewer
            ? await _saved.FindSavedListingIdsAsync(
                viewer, [.. listings.Select(listing => listing.Id)], cancellationToken)
            : null;

        return
        [
            .. listings.Select(listing => ListingMapper.ToCard(
                listing,
                sellers.GetValueOrDefault(listing.SellerId),
                saved?.Contains(listing.Id.Value)))
        ];
    }
}
