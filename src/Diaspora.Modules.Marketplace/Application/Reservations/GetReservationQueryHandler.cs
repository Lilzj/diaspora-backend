using Diaspora.Application.Abstractions;
using Diaspora.Modules.Marketplace.Application.Mapping;
using Diaspora.Modules.Marketplace.Application.Services;
using Diaspora.Modules.Marketplace.Contracts.V1.Responses;
using Diaspora.Modules.Marketplace.Domain.Errors;
using Diaspora.Modules.Marketplace.Domain.Identifiers;
using Diaspora.Modules.Marketplace.Domain.Listings;
using Diaspora.Modules.Marketplace.Domain.Reservations;
using Diaspora.Modules.Marketplace.Domain.Sellers;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Marketplace.Application.Reservations;

/// <summary>
/// Reads one reservation.
/// <para>
/// Readable only by its two parties, and not-found to anybody else — a 403 would confirm that two
/// named people arranged to meet.
/// </para>
/// </summary>
public sealed class GetReservationQueryHandler
    : IQueryHandler<GetReservationQuery, ReservationResponse>
{
    private readonly IListingReservationRepository _reservations;
    private readonly IMarketplaceListingRepository _listings;
    private readonly ISellerProfileRepository _sellers;
    private readonly MarketplaceAuthorizationService _authorization;

    public GetReservationQueryHandler(
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

    public async Task<Result<ReservationResponse>> Handle(
        GetReservationQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var caller = _authorization.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<ReservationResponse>(caller.Error);
        }

        var reservation = await _reservations.GetForReadAsync(
            ListingReservationId.From(query.ReservationId), cancellationToken);

        if (reservation is null)
        {
            return Result.Failure<ReservationResponse>(MarketplaceErrors.ReservationNotFound);
        }

        var seller = await _sellers.GetForReadAsync(reservation.SellerId, cancellationToken);

        if (!reservation.Involves(caller.Value, seller?.OwnerUserId ?? caller.Value)
            || (reservation.BuyerUserId != caller.Value
                && seller?.OwnerUserId != caller.Value))
        {
            return Result.Failure<ReservationResponse>(MarketplaceErrors.ReservationNotFound);
        }

        var listing = await _listings.GetForReadAsync(reservation.ListingId, cancellationToken);

        return Result.Success(ReservationMapper.ToResponse(
            reservation, caller.Value, listing, seller));
    }
}
