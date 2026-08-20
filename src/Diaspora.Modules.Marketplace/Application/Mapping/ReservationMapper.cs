using Diaspora.Modules.Marketplace.Contracts.V1.Responses;
using Diaspora.Modules.Marketplace.Domain.Enums;
using Diaspora.Modules.Marketplace.Domain.Listings;
using Diaspora.Modules.Marketplace.Domain.Policies;
using Diaspora.Modules.Marketplace.Domain.Reservations;
using Diaspora.Modules.Marketplace.Domain.Sellers;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Marketplace.Application.Mapping;

/// <summary>
/// Turns reservations into wire responses.
/// <para>
/// <b><c>AvailableActions</c> is computed here for the same reason <c>IsMyTurn</c> is on an
/// offer.</b> Which of acknowledge, complete and cancel a member may press depends on the status
/// and on which side they are, and a client that derived it would eventually offer "complete" to a
/// buyer on a reservation the seller has not acknowledged.
/// </para>
/// </summary>
internal static class ReservationMapper
{
    public static ReservationResponse ToResponse(
        ListingReservation reservation,
        UserId? viewerId,
        MarketplaceListing? listing,
        SellerProfile? seller)
    {
        ArgumentNullException.ThrowIfNull(reservation);

        var isBuyer = viewerId is not null && reservation.BuyerUserId == viewerId;
        var isSeller = viewerId is not null && seller?.OwnerUserId == viewerId;

        return new ReservationResponse
        {
            ReservationId = reservation.Id.Value,
            ListingId = reservation.ListingId.Value,
            ListingTitle = listing?.Title.Value ?? string.Empty,
            ListingCoverMediaId = listing?.CoverImage?.MediaId,
            Seller = seller is null
                ? SellerMapper.UnknownSeller(reservation.SellerId.Value)
                : SellerMapper.ToSummary(seller),
            BuyerUserId = reservation.BuyerUserId.Value,
            Status = reservation.Status.ToString(),
            Quantity = reservation.Quantity,
            AgreedAmount = reservation.AgreedAmount,
            Currency = reservation.CurrencyCode,
            OfferId = reservation.OfferId?.Value,
            CollectFrom = reservation.Window.From,
            CollectUntil = reservation.Window.Until,
            CollectionPlace = reservation.Window.Place,
            CreatedAt = reservation.CreatedAt,
            CompletedAt = reservation.CompletedAt,
            ClosureReason = reservation.ClosureReason,
            MyParty = isSeller ? nameof(OfferParty.Seller)
                : isBuyer ? nameof(OfferParty.Buyer)
                : string.Empty,
            AvailableActions = AvailableActions(reservation, isBuyer, isSeller),
            History = [.. reservation.History.Select(ToEvent)]
        };
    }

    public static ReservationEventResponse ToEvent(ReservationEvent entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        return new ReservationEventResponse
        {
            Status = entry.Status.ToString(),
            Reason = entry.Reason,

            // No actor means the sweep recorded it. That is the honest way to say "the clock did
            // this", and it is what keeps an expiry off a seller's cancellation rate.
            WasAutomatic = entry.ActorId is null,
            OccurredAt = entry.OccurredAt
        };
    }

    /// <summary>
    /// What this member may do next.
    /// <para>
    /// Acknowledging is the seller's; completing is either party's once the seller has
    /// acknowledged; cancelling is both parties' at any point while stock is held, and is never
    /// withheld — a reservation nobody could cancel would hold stock hostage until its window
    /// lapsed.
    /// </para>
    /// </summary>
    private static List<string> AvailableActions(
        ListingReservation reservation, bool isBuyer, bool isSeller)
    {
        if (!ReservationStatusTransitions.IsHoldingStock(reservation.Status)
            || (!isBuyer && !isSeller))
        {
            return [];
        }


        var actions = new List<string>();

        if (isSeller && reservation.Status == ReservationStatus.Pending)
        {
            actions.Add("Acknowledge");
        }

        if (reservation.Status == ReservationStatus.AwaitingCollection)
        {
            actions.Add("Complete");
        }

        actions.Add("Cancel");

        return actions;
    }

    /// <summary>The reservation status an action word names. Null when it names none.</summary>
    public static string? NormalizeAction(string? action) => action?.Trim() switch
    {
        var value when string.Equals(value, "Acknowledge", StringComparison.OrdinalIgnoreCase) =>
            "Acknowledge",
        var value when string.Equals(value, "Complete", StringComparison.OrdinalIgnoreCase) =>
            "Complete",
        var value when string.Equals(value, "Cancel", StringComparison.OrdinalIgnoreCase) =>
            "Cancel",
        _ => null
    };
}
