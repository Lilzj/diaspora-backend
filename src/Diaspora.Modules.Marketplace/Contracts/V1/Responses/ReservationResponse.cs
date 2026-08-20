namespace Diaspora.Modules.Marketplace.Contracts.V1.Responses;

/// <summary>
/// A hold on stock, with everything that has happened to it.
/// <para>
/// The collection window and place are the two fields both parties actually act on, and they are
/// sent to both. Everything else is a record.
/// </para>
/// </summary>
public sealed record ReservationResponse
{
    public required Guid ReservationId { get; init; }

    public required Guid ListingId { get; init; }

    public required string ListingTitle { get; init; }

    public Guid? ListingCoverMediaId { get; init; }

    public required SellerSummaryResponse Seller { get; init; }

    public required Guid BuyerUserId { get; init; }

    /// <summary>
    /// <c>Pending</c>, <c>AwaitingCollection</c>, <c>Completed</c>, <c>Cancelled</c> or
    /// <c>Expired</c>.
    /// </summary>
    public required string Status { get; init; }

    public required int Quantity { get; init; }

    /// <summary>What the parties agreed. Null when the reservation was at the asking price.</summary>
    public decimal? AgreedAmount { get; init; }

    public string? Currency { get; init; }

    /// <summary>The negotiation it came out of, when it came out of one.</summary>
    public Guid? OfferId { get; init; }

    public DateTimeOffset? CollectFrom { get; init; }

    /// <summary>When the hold lapses. The clock the expiry sweep watches.</summary>
    public DateTimeOffset? CollectUntil { get; init; }

    /// <summary>Where, in the parties' own words.</summary>
    public string? CollectionPlace { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset? CompletedAt { get; init; }

    public string? ClosureReason { get; init; }

    /// <summary>Which side the caller is on — <c>Buyer</c> or <c>Seller</c>.</summary>
    public required string MyParty { get; init; }

    /// <summary>What the caller may do next: <c>Acknowledge</c>, <c>Complete</c>, <c>Cancel</c>.</summary>
    public required IReadOnlyList<string> AvailableActions { get; init; }

    public required IReadOnlyList<ReservationEventResponse> History { get; init; }
}
