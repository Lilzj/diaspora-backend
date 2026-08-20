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
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Marketplace.Application.Reservations;

/// <summary>
/// Applies a reservation transition on behalf of whichever party may make it.
/// <para>
/// <b>Acknowledging is the seller's, completing is either party's, cancelling is both parties'.</b>
/// The asymmetry is deliberate: only a seller can agree to hand something over, either party can
/// confirm that they met, and neither should ever be trapped in an arrangement they want out of.
/// </para>
/// <para>
/// The stock settles in the handler for the status event this raises, which is the one place
/// completion converts held units to sold and cancellation returns them.
/// </para>
/// </summary>
public sealed class UpdateReservationCommandHandler
    : ICommandHandler<UpdateReservationCommand, ReservationResponse>
{
    private readonly IListingReservationRepository _reservations;
    private readonly IMarketplaceListingRepository _listings;
    private readonly ISellerProfileRepository _sellers;
    private readonly IUnitOfWork _unitOfWork;
    private readonly MarketplaceAuthorizationService _authorization;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;

    public UpdateReservationCommandHandler(
        IListingReservationRepository reservations,
        IMarketplaceListingRepository listings,
        ISellerProfileRepository sellers,
        IUnitOfWork unitOfWork,
        MarketplaceAuthorizationService authorization,
        IGuidGenerator guids,
        IClock clock)
    {
        _reservations = reservations;
        _listings = listings;
        _sellers = sellers;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _guids = guids;
        _clock = clock;
    }

    public async Task<Result<ReservationResponse>> Handle(
        UpdateReservationCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = _authorization.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<ReservationResponse>(caller.Error);
        }

        var reservation = await _reservations.GetAsync(
            ListingReservationId.From(command.ReservationId), cancellationToken);

        if (reservation is null)
        {
            return Result.Failure<ReservationResponse>(MarketplaceErrors.ReservationNotFound);
        }

        var seller = await _sellers.GetAsync(reservation.SellerId, cancellationToken);

        var isBuyer = reservation.BuyerUserId == caller.Value;
        var isSeller = seller?.OwnerUserId == caller.Value;

        if (!isBuyer && !isSeller)
        {
            return Result.Failure<ReservationResponse>(
                MarketplaceErrors.NotReservationParty);
        }

        var action = ReservationMapper.NormalizeAction(command.Request.Action);

        if (action is null)
        {
            return Result.Failure<ReservationResponse>(
                MarketplaceErrors.ReservationTransitionInvalid);
        }

        var now = _clock.UtcNow;
        var entryId = _guids.NewSequentialGuid();

        var applied = action switch
        {
            "Acknowledge" when isSeller => await AcknowledgeAsync(
                reservation, caller.Value, command, now, entryId),

            // Only the seller may agree to hand something over. A buyer acknowledging their own
            // reservation would be agreeing with themselves.
            "Acknowledge" => Result.Failure(MarketplaceErrors.NotReservationParty),

            "Complete" => reservation.Complete(caller.Value, now, entryId),
            _ => reservation.Cancel(caller.Value, command.Request.Reason, now, entryId)
        };

        if (applied.IsFailure)
        {
            return Result.Failure<ReservationResponse>(applied.Error);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var listing = await _listings.GetForReadAsync(reservation.ListingId, cancellationToken);

        return Result.Success(ReservationMapper.ToResponse(
            reservation, caller.Value, listing, seller));
    }

    /// <summary>
    /// The seller agrees, and may restate the collection window in the same call — which is exactly
    /// when a seller says "come Saturday between ten and two".
    /// </summary>
    private static async Task<Result> AcknowledgeAsync(
        ListingReservation reservation,
        Diaspora.SharedKernel.Identity.UserId actor,
        UpdateReservationCommand command,
        DateTimeOffset now,
        Guid entryId)
    {
        CollectionWindow? window = null;

        if (command.Request.CollectFrom is not null
            || command.Request.CollectUntil is not null
            || !string.IsNullOrWhiteSpace(command.Request.CollectionPlace))
        {
            var built = CollectionWindow.Create(
                command.Request.CollectFrom ?? reservation.Window.From,
                command.Request.CollectUntil ?? reservation.Window.Until,
                command.Request.CollectionPlace ?? reservation.Window.Place);

            if (built.IsFailure)
            {
                return Result.Failure(built.Error);
            }

            window = built.Value;
        }

        return await Task.FromResult(reservation.Acknowledge(actor, window, now, entryId));
    }
}
