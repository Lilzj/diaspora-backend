using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Marketplace.Application.Mapping;
using Diaspora.Modules.Marketplace.Application.Services;
using Diaspora.Modules.Marketplace.Contracts.V1.Responses;
using Diaspora.Modules.Marketplace.Domain.Enums;
using Diaspora.Modules.Marketplace.Domain.Errors;
using Diaspora.Modules.Marketplace.Domain.Identifiers;
using Diaspora.Modules.Marketplace.Domain.Listings;
using Diaspora.Modules.Marketplace.Domain.Offers;
using Diaspora.Modules.Marketplace.Domain.Reservations;
using Diaspora.Modules.Marketplace.Domain.Sellers;
using Diaspora.Modules.Marketplace.Domain.ValueObjects;
using Diaspora.Modules.Marketplace.Options;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Marketplace.Application.Offers;

/// <summary>
/// Settles a negotiation, and on acceptance turns it into a hold on real stock.
/// <para>
/// <b>Acceptance does three things in one transaction and the order is load-bearing.</b> The stock
/// is held on the listing first, because that is the step that can fail — somebody else may have
/// bought the last one while the seller was deciding. Only then does the negotiation settle and the
/// reservation open, so a failed hold leaves the negotiation exactly as it was rather than accepted
/// against nothing.
/// </para>
/// <para>
/// <b>Accepting does not sell anything.</b> The sale happens when the reservation completes. A
/// module where acceptance marked an item sold would be one where a buyer who never turned up still
/// counts as a sale, and where the seller could not re-list the item without falsifying their own
/// history.
/// </para>
/// </summary>
public sealed class SettleOfferCommandHandler : ICommandHandler<SettleOfferCommand, OfferResponse>
{
    private readonly IListingOfferRepository _offers;
    private readonly IMarketplaceListingRepository _listings;
    private readonly ISellerProfileRepository _sellers;
    private readonly IListingReservationRepository _reservations;
    private readonly IListingStockGuard _stock;
    private readonly IUnitOfWork _unitOfWork;
    private readonly MarketplaceAuthorizationService _authorization;
    private readonly IOptions<MarketplaceOptions> _options;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;

    public SettleOfferCommandHandler(
        IListingOfferRepository offers,
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
        _offers = offers;
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

    public async Task<Result<OfferResponse>> Handle(
        SettleOfferCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = _authorization.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<OfferResponse>(caller.Error);
        }

        var offer = await _offers.GetAsync(
            ListingOfferId.From(command.OfferId), cancellationToken);

        if (offer is null || !offer.Involves(caller.Value))
        {
            return Result.Failure<OfferResponse>(MarketplaceErrors.OfferNotFound);
        }

        var decision = OfferMapper.ParseDecision(command.Request.Decision);

        if (decision is null)
        {
            return Result.Failure<OfferResponse>(MarketplaceErrors.OfferAmountInvalid);
        }

        var now = _clock.UtcNow;
        Guid? reservationId = null;

        var settled = decision switch
        {
            OfferStatus.Accepted => await AcceptAsync(
                offer, caller.Value, command, now, id => reservationId = id, cancellationToken),
            OfferStatus.Rejected => offer.Reject(
                caller.Value, command.Request.Reason, now, _guids.NewSequentialGuid()),
            _ => offer.Withdraw(
                caller.Value, command.Request.Reason, now, _guids.NewSequentialGuid())
        };

        if (settled.IsFailure)
        {
            return Result.Failure<OfferResponse>(settled.Error);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var listing = await _listings.GetForReadAsync(offer.ListingId, cancellationToken);
        var seller = await _sellers.GetForReadAsync(offer.SellerId, cancellationToken);

        return Result.Success(
            OfferMapper.ToResponse(offer, caller.Value, listing, seller, reservationId));
    }

    /// <summary>
    /// Holds the stock, settles the negotiation and opens the reservation — in that order.
    /// </summary>
    private async Task<Result> AcceptAsync(
        ListingOffer offer,
        Diaspora.SharedKernel.Identity.UserId actor,
        SettleOfferCommand command,
        DateTimeOffset now,
        Action<Guid> recordReservation,
        CancellationToken cancellationToken)
    {
        var listing = await _listings.GetAsync(offer.ListingId, cancellationToken);

        if (listing is null)
        {
            return Result.Failure(MarketplaceErrors.ListingNotFound);
        }

        // The step that can fail. Somebody else may have taken the last one while the seller was
        // deciding, and finding that out before the negotiation settles is what keeps the two
        // consistent.
        var held = await _stock.HoldAsync(
            listing, offer.Terms.Quantity, now, cancellationToken);

        if (held.IsFailure)
        {
            return held;
        }

        var accepted = offer.Accept(actor, now, _guids.NewSequentialGuid());

        if (accepted.IsFailure)
        {
            return accepted;
        }

        var window = CollectionWindow.Create(
            command.Request.CollectFrom,
            command.Request.CollectUntil
                ?? now.AddHours(Math.Max(1, _options.Value.DefaultReservationHours)),
            command.Request.CollectionPlace);

        if (window.IsFailure)
        {
            return Result.Failure(window.Error);
        }

        var reservation = ListingReservation.Open(
            ListingReservationId.From(_guids.NewSequentialGuid()),
            listing.Id,
            listing.SellerId,
            offer.BuyerUserId,
            offer.Terms.Quantity,
            window.Value,
            now,
            _guids.NewSequentialGuid(),
            offer.Id,
            offer.Terms.Amount,
            offer.Terms.CurrencyCode);

        if (reservation.IsFailure)
        {
            return Result.Failure(reservation.Error);
        }

        await _reservations.AddAsync(reservation.Value, cancellationToken);

        recordReservation(reservation.Value.Id.Value);

        return Result.Success();
    }
}
