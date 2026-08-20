using Diaspora.Application.Abstractions;
using Diaspora.Modules.Marketplace.Application.Mapping;
using Diaspora.Modules.Marketplace.Application.Services;
using Diaspora.Modules.Marketplace.Contracts.V1.Responses;
using Diaspora.Modules.Marketplace.Domain.Enums;
using Diaspora.Modules.Marketplace.Domain.Errors;
using Diaspora.Modules.Marketplace.Domain.Listings;
using Diaspora.Modules.Marketplace.Domain.Offers;
using Diaspora.Modules.Marketplace.Domain.Reservations;
using Diaspora.Modules.Marketplace.Domain.Sellers;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Marketplace.Application.Dashboards;

/// <summary>
/// Assembles the seller dashboard.
/// <para>
/// <b>The three "needs attention" numbers at the top are the ones that cost a seller money when
/// they are ignored</b>: an unanswered offer expires, an unacknowledged reservation lapses, and a
/// listing about to expire is one that stops being seen tonight. Everything below them is standing
/// context.
/// </para>
/// <para>
/// A suspended seller can still open this. Somebody who has been barred from trading needs to see
/// their listings and read why more than anybody.
/// </para>
/// </summary>
public sealed class GetSellerDashboardQueryHandler
    : IQueryHandler<GetSellerDashboardQuery, SellerDashboardResponse>
{
    private const int RailSize = 10;

    private readonly IMarketplaceListingRepository _listings;
    private readonly IListingOfferRepository _offers;
    private readonly IListingReservationRepository _reservations;
    private readonly MarketplaceAuthorizationService _authorization;
    private readonly IClock _clock;

    public GetSellerDashboardQueryHandler(
        IMarketplaceListingRepository listings,
        IListingOfferRepository offers,
        IListingReservationRepository reservations,
        MarketplaceAuthorizationService authorization,
        IClock clock)
    {
        _listings = listings;
        _offers = offers;
        _reservations = reservations;
        _authorization = authorization;
        _clock = clock;
    }

    public async Task<Result<SellerDashboardResponse>> Handle(
        GetSellerDashboardQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var seller = await _authorization.FindCallerSellerAsync(cancellationToken);

        if (seller is null)
        {
            return Result.Failure<SellerDashboardResponse>(MarketplaceErrors.NotSeller);
        }

        var now = _clock.UtcNow;

        var awaitingOffers = await _offers.QueryAsync(
            new OfferQueryFilter
            {
                SellerId = seller.Id,
                OpenOnly = true,
                AwaitingParty = OfferParty.Seller,
                Limit = RailSize
            },
            cursor: null,
            RailSize,
            cancellationToken);

        var pendingReservations = await _reservations.QueryAsync(
            new ReservationQueryFilter
            {
                SellerId = seller.Id,
                ActiveOnly = true,
                Limit = RailSize
            },
            cursor: null,
            RailSize,
            cancellationToken);

        var listings = await _listings.GetActiveBySellerAsync(
            seller.Id, 500, cancellationToken);

        var recent = await _listings.QueryAsync(
            await _authorization.ApplyAudienceAsync(
                new ListingQueryFilter
                {
                    SellerId = seller.Id,
                    Sort = ListingSortOrder.Newest,
                    Limit = RailSize
                },
                includeOwn: true,
                cancellationToken),
            cursor: null,
            RailSize,
            cancellationToken);

        var offerCards = await BuildOfferCardsAsync(
            awaitingOffers.Items, seller, cancellationToken);

        var reservationCards = pendingReservations.Items
            .Select(reservation => ReservationMapper.ToResponse(
                reservation,
                seller.OwnerUserId,
                listings.FirstOrDefault(listing => listing.Id == reservation.ListingId),
                seller))
            .ToList();

        return Result.Success(new SellerDashboardResponse
        {
            Seller = SellerMapper.ToResponse(
                seller, canManage: true, _authorization.IsStaff),
            OffersAwaitingYou = offerCards.Count,
            ReservationsAwaitingYou = pendingReservations.Items
                .Count(reservation => reservation.Status == ReservationStatus.Pending),
            ListingsExpiringSoon = listings.Count(listing =>
                listing.ExpiresAt is { } expiry && expiry <= now.AddDays(1)),
            ActiveListings = listings.Count(listing => listing.Status == ListingStatus.Active),
            DraftListings = recent.Items.Count(
                listing => listing.Status == ListingStatus.Draft),
            ReservedListings = listings.Count(
                listing => listing.Status == ListingStatus.Reserved),
            SoldListings = seller.Statistics.SoldListings,
            TotalViews = listings.Sum(listing => listing.Engagement.ViewCount),
            TotalSaves = listings.Sum(listing => listing.Engagement.SaveCount),
            PendingOffers = offerCards,
            ActiveReservations = reservationCards,
            RecentListings =
            [
                .. recent.Items.Select(listing => ListingMapper.ToCard(listing, seller))
            ]
        });
    }

    private async Task<List<OfferCardResponse>> BuildOfferCardsAsync(
        IReadOnlyList<ListingOffer> offers,
        SellerProfile seller,
        CancellationToken cancellationToken)
    {
        if (offers.Count == 0)
        {
            return [];
        }

        var listings = await _listings.GetManyAsync(
            [.. offers.Select(offer => offer.ListingId).Distinct()], cancellationToken);

        var byId = listings.ToDictionary(listing => listing.Id);

        return
        [
            .. offers.Select(offer => OfferMapper.ToCard(
                offer, seller.OwnerUserId, byId.GetValueOrDefault(offer.ListingId)))
        ];
    }
}
