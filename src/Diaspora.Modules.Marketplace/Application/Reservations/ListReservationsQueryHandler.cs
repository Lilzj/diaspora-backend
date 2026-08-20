using Diaspora.Application.Abstractions;
using Diaspora.Modules.Marketplace.Application.Mapping;
using Diaspora.Modules.Marketplace.Application.Services;
using Diaspora.Modules.Marketplace.Contracts.V1.Responses;
using Diaspora.Modules.Marketplace.Domain.Listings;
using Diaspora.Modules.Marketplace.Domain.Policies;
using Diaspora.Modules.Marketplace.Domain.Reservations;
using Diaspora.Modules.Marketplace.Domain.Sellers;
using Diaspora.SharedKernel.Pagination;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Marketplace.Application.Reservations;

/// <summary>Reads the caller's reservations from whichever side they asked for.</summary>
public sealed class ListReservationsQueryHandler
    : IQueryHandler<ListReservationsQuery, CursorPagedResult<ReservationResponse>>
{
    private readonly IListingReservationRepository _reservations;
    private readonly IMarketplaceListingRepository _listings;
    private readonly ISellerProfileRepository _sellers;
    private readonly MarketplaceAuthorizationService _authorization;

    public ListReservationsQueryHandler(
        IListingReservationRepository reservations,
        IMarketplaceListingRepository listings,
        ISellerProfileRepository sellers,
        MarketplaceAuthorizationService authorization)
    {
        _reservations = reservations;
        _listings = listings;
        _sellers = sellers;
        _authorization = authorization;
    }

    public async Task<Result<CursorPagedResult<ReservationResponse>>> Handle(
        ListReservationsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var caller = _authorization.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<CursorPagedResult<ReservationResponse>>(caller.Error);
        }

        var selling = string.Equals(query.Side, "Selling", StringComparison.OrdinalIgnoreCase);

        var sellerProfile = selling
            ? await _authorization.FindCallerSellerAsync(cancellationToken)
            : null;

        if (selling && sellerProfile is null)
        {
            return Result.Success(
                CursorPagedResult<ReservationResponse>.Create([], null, hasMore: false));
        }

        var limit = Math.Clamp(
            query.Limit ?? MarketplaceLimits.DefaultPageSize, 1, MarketplaceLimits.MaxPageSize);

        var filter = new ReservationQueryFilter
        {
            SellerId = selling ? sellerProfile!.Id : null,
            BuyerUserId = selling ? null : caller.Value,
            ActiveOnly = query.ActiveOnly,
            Limit = limit
        };

        var page = await _reservations.QueryAsync(
            filter, query.Cursor, limit, cancellationToken);

        var listings = await _listings.GetManyAsync(
            [.. page.Items.Select(reservation => reservation.ListingId).Distinct()],
            cancellationToken);

        var byId = listings.ToDictionary(listing => listing.Id);

        var sellers = await _sellers.GetManyAsync(
            [.. page.Items.Select(reservation => reservation.SellerId).Distinct()],
            cancellationToken);

        var items = page.Items
            .Select(reservation => ReservationMapper.ToResponse(
                reservation,
                caller.Value,
                byId.GetValueOrDefault(reservation.ListingId),
                sellers.GetValueOrDefault(reservation.SellerId)))
            .ToList();

        return Result.Success(
            CursorPagedResult<ReservationResponse>.Create(
                items, page.NextCursor, page.HasMore));
    }
}
