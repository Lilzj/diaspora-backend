using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Marketplace.Application.Mapping;
using Diaspora.Modules.Marketplace.Application.Services;
using Diaspora.Modules.Marketplace.Contracts.V1.Responses;
using Diaspora.Modules.Marketplace.Domain.Errors;
using Diaspora.Modules.Marketplace.Domain.Identifiers;
using Diaspora.Modules.Marketplace.Domain.Listings;
using Diaspora.Modules.Marketplace.Domain.Reservations;
using Diaspora.Modules.Marketplace.Domain.Sellers;
using Diaspora.Modules.Marketplace.Domain.ValueObjects;
using Diaspora.Modules.Marketplace.Options;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Marketplace.Application.Reservations;

/// <summary>
/// Opens a reservation and takes the stock in the same transaction.
/// <para>
/// <b>The per-member ceiling is the most consequential check here.</b> Every open reservation is
/// stock nobody else can buy, so a member who could hold five hundred could empty a category for as
/// long as the windows lasted. It is an option rather than a constant because how much trust a
/// deployment extends is genuinely a deployment decision.
/// </para>
/// <para>
/// A default window is applied when the parties agreed none, so an item is never held indefinitely
/// by somebody who has gone quiet.
/// </para>
/// </summary>
public sealed class CreateReservationCommandHandler
    : ICommandHandler<CreateReservationCommand, ReservationResponse>
{
    private readonly IMarketplaceListingRepository _listings;
    private readonly ISellerProfileRepository _sellers;
    private readonly IListingReservationRepository _reservations;
    private readonly IListingStockGuard _stock;
    private readonly IUnitOfWork _unitOfWork;
    private readonly MarketplaceAuthorizationService _authorization;
    private readonly IOptions<MarketplaceOptions> _options;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;

    public CreateReservationCommandHandler(
        IMarketplaceListingRepository listings,
        ISellerProfileRepository sellers,
        IListingReservationRepository reservations,
        IListingStockGuard stock,
        IUnitOfWork unitOfWork,
        MarketplaceAuthorizationService authorization,
        IOptions<MarketplaceOptions> options,
        IGuidGenerator guids,
        IClock clock)
    {
        _listings = listings;
        _sellers = sellers;
        _reservations = reservations;
        _stock = stock;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _options = options;
        _guids = guids;
        _clock = clock;
    }

    public async Task<Result<ReservationResponse>> Handle(
        CreateReservationCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = _authorization.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<ReservationResponse>(caller.Error);
        }

        var listing = await _listings.GetAsync(
            MarketplaceListingId.From(command.ListingId), cancellationToken);

        if (listing is null || !await _authorization.CanSeeAsync(listing, cancellationToken))
        {
            return Result.Failure<ReservationResponse>(MarketplaceErrors.ListingNotFound);
        }

        var now = _clock.UtcNow;

        if (!listing.AcceptsReservations(now))
        {
            return Result.Failure<ReservationResponse>(
                MarketplaceErrors.ReservationsNotAccepted);
        }

        var seller = await _sellers.GetAsync(listing.SellerId, cancellationToken);

        if (seller is null || !seller.CanTrade)
        {
            return Result.Failure<ReservationResponse>(MarketplaceErrors.SellerSuspended);
        }

        if (seller.OwnerUserId == caller.Value)
        {
            return Result.Failure<ReservationResponse>(MarketplaceErrors.ReservationSelf);
        }

        var existing = await _reservations.GetActiveForBuyerAsync(
            listing.Id, caller.Value, cancellationToken);

        if (existing is not null)
        {
            return Result.Failure<ReservationResponse>(
                MarketplaceErrors.ReservationAlreadyOpen);
        }

        var held = await _reservations.CountActiveByBuyerAsync(caller.Value, cancellationToken);

        if (held >= _options.Value.MaxActiveReservationsPerBuyer)
        {
            return Result.Failure<ReservationResponse>(MarketplaceErrors.TooManyReservations);
        }

        var window = CollectionWindow.Create(
            command.Request.CollectFrom,
            command.Request.CollectUntil
                ?? now.AddHours(Math.Max(1, _options.Value.DefaultReservationHours)),
            command.Request.CollectionPlace);

        if (window.IsFailure)
        {
            return Result.Failure<ReservationResponse>(window.Error);
        }

        var quantity = command.Request.Quantity ?? 1;

        // The stock first, because it is the step that can fail. A reservation opened before the
        // hold would be one the platform believes in and the shelf does not. Through the guard
        // rather than the aggregate directly, so the database arbitrates the last unit rather than
        // two buyers each deciding correctly from the same stale count.
        var stock = await _stock.HoldAsync(listing, quantity, now, cancellationToken);

        if (stock.IsFailure)
        {
            return Result.Failure<ReservationResponse>(stock.Error);
        }

        var reservation = ListingReservation.Open(
            ListingReservationId.From(_guids.NewSequentialGuid()),
            listing.Id,
            listing.SellerId,
            caller.Value,
            quantity,
            window.Value,
            now,
            _guids.NewSequentialGuid(),
            offerId: null,
            listing.Price.Amount,
            listing.Price.CurrencyCode);

        if (reservation.IsFailure)
        {
            return Result.Failure<ReservationResponse>(reservation.Error);
        }

        await _reservations.AddAsync(reservation.Value, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(ReservationMapper.ToResponse(
            reservation.Value, caller.Value, listing, seller));
    }
}
