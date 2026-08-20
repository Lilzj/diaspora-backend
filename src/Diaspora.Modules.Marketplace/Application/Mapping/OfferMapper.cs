using Diaspora.Modules.Marketplace.Contracts.V1.Responses;
using Diaspora.Modules.Marketplace.Domain.Enums;
using Diaspora.Modules.Marketplace.Domain.Listings;
using Diaspora.Modules.Marketplace.Domain.Offers;
using Diaspora.Modules.Marketplace.Domain.Policies;
using Diaspora.Modules.Marketplace.Domain.Sellers;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Marketplace.Application.Mapping;

/// <summary>
/// Turns negotiations into wire responses.
/// <para>
/// <b><c>IsMyTurn</c> is computed here rather than left to the client.</b> Deriving it from the
/// status and the awaiting party is possible and is exactly the sort of derivation two clients get
/// differently — and the one that gets it wrong shows an "accept" button to somebody who is not
/// allowed to press it.
/// </para>
/// </summary>
internal static class OfferMapper
{
    /// <summary>The full negotiation, with every round.</summary>
    public static OfferResponse ToResponse(
        ListingOffer offer,
        UserId? viewerId,
        MarketplaceListing? listing,
        SellerProfile? seller,
        Guid? reservationId = null)
    {
        ArgumentNullException.ThrowIfNull(offer);

        var party = viewerId is null ? null : offer.PartyOf(viewerId);

        return new OfferResponse
        {
            OfferId = offer.Id.Value,
            ListingId = offer.ListingId.Value,
            ListingTitle = listing?.Title.Value ?? string.Empty,
            ListingCoverMediaId = listing?.CoverImage?.MediaId,
            Seller = seller is null
                ? SellerMapper.UnknownSeller(offer.SellerId.Value)
                : SellerMapper.ToSummary(seller),
            BuyerUserId = offer.BuyerUserId.Value,
            Status = offer.Status.ToString(),
            Amount = offer.Terms.Amount,
            Currency = offer.Terms.CurrencyCode,
            Quantity = offer.Terms.Quantity,
            AskingPrice = listing?.Price.Amount,
            AwaitingParty = offer.AwaitingParty.ToString(),
            IsMyTurn = offer.IsOpen && party == offer.AwaitingParty,
            MyParty = party?.ToString() ?? string.Empty,
            Rounds = offer.Rounds,
            RoundsRemaining = Math.Max(0, MarketplaceLimits.MaxNegotiationRounds - offer.Rounds),
            ExpiresAt = offer.ExpiresAt,
            CreatedAt = offer.CreatedAt,
            SettledAt = offer.SettledAt,
            SettlementReason = offer.SettlementReason,
            ReservationId = reservationId,
            History = [.. offer.History.Select(ToEntry)]
        };
    }

    /// <summary>
    /// A negotiation as a dashboard list renders it. No history, which is what makes a seller with
    /// two hundred open negotiations a page rather than a download.
    /// </summary>
    public static OfferCardResponse ToCard(
        ListingOffer offer, UserId? viewerId, MarketplaceListing? listing)
    {
        ArgumentNullException.ThrowIfNull(offer);

        var party = viewerId is null ? null : offer.PartyOf(viewerId);

        return new OfferCardResponse
        {
            OfferId = offer.Id.Value,
            ListingId = offer.ListingId.Value,
            ListingTitle = listing?.Title.Value ?? string.Empty,
            ListingCoverMediaId = listing?.CoverImage?.MediaId,
            Status = offer.Status.ToString(),
            Amount = offer.Terms.Amount,
            Currency = offer.Terms.CurrencyCode,
            Quantity = offer.Terms.Quantity,
            AwaitingParty = offer.AwaitingParty.ToString(),
            IsMyTurn = offer.IsOpen && party == offer.AwaitingParty,
            Rounds = offer.Rounds,
            ExpiresAt = offer.ExpiresAt,
            LastActivityAt = offer.LastActivityAt,
            CreatedAt = offer.CreatedAt
        };
    }

    public static NegotiationEntryResponse ToEntry(NegotiationEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        return new NegotiationEntryResponse
        {
            Round = entry.Round,
            Action = entry.Action.ToString(),
            Party = entry.Party.ToString(),
            Amount = entry.Amount,
            Currency = entry.CurrencyCode,
            Quantity = entry.Quantity,
            Message = entry.Message,
            OccurredAt = entry.OccurredAt
        };
    }

    /// <summary>Whether a settlement decision word names an acceptance.</summary>
    public static bool IsAcceptance(string? decision) =>
        string.Equals(decision?.Trim(), "Accept", StringComparison.OrdinalIgnoreCase);

    /// <summary>The offer status a settlement decision word names. Null when it names none.</summary>
    public static OfferStatus? ParseDecision(string? decision) => decision?.Trim() switch
    {
        var value when string.Equals(value, "Accept", StringComparison.OrdinalIgnoreCase) =>
            OfferStatus.Accepted,
        var value when string.Equals(value, "Reject", StringComparison.OrdinalIgnoreCase) =>
            OfferStatus.Rejected,
        var value when string.Equals(value, "Decline", StringComparison.OrdinalIgnoreCase) =>
            OfferStatus.Rejected,
        var value when string.Equals(value, "Withdraw", StringComparison.OrdinalIgnoreCase) =>
            OfferStatus.Withdrawn,
        _ => null
    };
}
