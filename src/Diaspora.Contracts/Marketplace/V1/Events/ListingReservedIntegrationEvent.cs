using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Marketplace.V1.Events;

/// <summary>
/// Stock was held for a named buyer.
/// <para>
/// <b>Published on every reservation, not only on the one that empties the shelf.</b> A listing of
/// six chairs with two held is still available, and a consumer that only heard about the last hold
/// would show six as available until the moment all six vanished.
/// </para>
/// <para>
/// <see cref="RemainingQuantity"/> is what a card renders and what tells a discovery consumer
/// whether to keep showing the listing at all. Zero means the item is off the market pending
/// collection — not sold.
/// </para>
/// </summary>
public sealed record ListingReservedIntegrationEvent : IntegrationEvent
{
    public required Guid ReservationId { get; init; }

    public required Guid ListingId { get; init; }

    public required Guid SellerId { get; init; }

    public required Guid BuyerUserId { get; init; }

    public required int Quantity { get; init; }

    /// <summary>Units still available after the hold. Zero means nothing is left to commit to.</summary>
    public required int RemainingQuantity { get; init; }

    /// <summary>When the hold lapses. Null when the parties agreed no window.</summary>
    public DateTimeOffset? ExpiresAt { get; init; }

    public required DateTimeOffset ReservedAt { get; init; }

    /// <summary>
    /// The member behind the seller profile — B23.x Wave 12.
    /// <para>
    /// Same field, same reason and same rule as on <c>ListingPublishedIntegrationEvent</c>: a
    /// seller-scoped automation is owned by the member, and a payload naming only the profile cannot
    /// be matched to one. "A reservation on one of my listings is ending" is the trigger that needed
    /// it.
    /// </para>
    /// <para>Appended, never inserted. Null on events published before Wave 12.</para>
    /// </summary>
    public Guid? SellerUserId { get; init; }
}
