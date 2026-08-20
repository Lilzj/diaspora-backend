namespace Diaspora.Modules.Marketplace.Contracts.V1.Responses;

/// <summary>
/// A negotiation in full, with every round of it.
/// <para>
/// <b><see cref="History"/> is the brief's "complete negotiation history" and it is the reason this
/// response is worth having.</b> A response carrying only the current amount would make a disputed
/// sale unarguable: what matters is what was said, in order, by whom.
/// </para>
/// <para>
/// <see cref="IsMyTurn"/> is what the client's action bar switches on. Deriving it client-side from
/// the party and the status is possible and is the sort of derivation two clients get differently.
/// </para>
/// </summary>
public sealed record OfferResponse
{
    public required Guid OfferId { get; init; }

    public required Guid ListingId { get; init; }

    public required string ListingTitle { get; init; }

    /// <summary>The listing's cover image, by reference, so the card needs no second read.</summary>
    public Guid? ListingCoverMediaId { get; init; }

    public required SellerSummaryResponse Seller { get; init; }

    public required Guid BuyerUserId { get; init; }

    /// <summary>
    /// <c>Pending</c>, <c>Countered</c>, <c>Accepted</c>, <c>Rejected</c>, <c>Withdrawn</c> or
    /// <c>Expired</c>.
    /// </summary>
    public required string Status { get; init; }

    /// <summary>What is on the table right now.</summary>
    public required decimal Amount { get; init; }

    public required string Currency { get; init; }

    public required int Quantity { get; init; }

    /// <summary>The seller's asking price, so a client can render the gap.</summary>
    public decimal? AskingPrice { get; init; }

    /// <summary><c>Buyer</c> or <c>Seller</c>. Meaningless once settled.</summary>
    public required string AwaitingParty { get; init; }

    /// <summary>Whether it is the caller's move. What the action bar switches on.</summary>
    public required bool IsMyTurn { get; init; }

    /// <summary>Which side the caller is on.</summary>
    public required string MyParty { get; init; }

    public required int Rounds { get; init; }

    /// <summary>Moves left before the negotiation must be settled or abandoned.</summary>
    public required int RoundsRemaining { get; init; }

    public DateTimeOffset? ExpiresAt { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset? SettledAt { get; init; }

    public string? SettlementReason { get; init; }

    /// <summary>The reservation an acceptance created, when it created one.</summary>
    public Guid? ReservationId { get; init; }

    public required IReadOnlyList<NegotiationEntryResponse> History { get; init; }
}
