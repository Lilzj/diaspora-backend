using Diaspora.Modules.Marketplace.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Marketplace.Domain.Events;

/// <summary>
/// Raised when a buyer takes stock off the market.
/// <para>
/// <b>The stock is already held by the time this is raised.</b> The hold happens inside the same
/// unit of work as the reservation's creation, not in a handler for this event: a reservation that
/// existed for even a moment without its hold is a window in which a second buyer can take the
/// same item.
/// </para>
/// </summary>
public sealed record ReservationCreatedDomainEvent : DomainEvent
{
    public required ListingReservationId ReservationId { get; init; }

    public required MarketplaceListingId ListingId { get; init; }

    public required SellerProfileId SellerId { get; init; }

    public required UserId BuyerUserId { get; init; }

    public required int Quantity { get; init; }

    /// <summary>When the hold lapses. Null when the parties agreed no window.</summary>
    public DateTimeOffset? ExpiresAt { get; init; }

    public required DateTimeOffset ReservedAt { get; init; }
}
