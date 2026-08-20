using Diaspora.Modules.Marketplace.Domain.Enums;
using Diaspora.Modules.Marketplace.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Marketplace.Domain.Events;

/// <summary>
/// Raised on every reservation transition.
/// <para>
/// <b>The handler for this is what settles the listing's stock, and it is the one place that
/// happens.</b> Completion converts held units to sold; cancellation and expiry return them.
/// Spreading that across three handlers would be three chances to forget one, and the forgotten
/// one is stock that is neither on the market nor accounted for.
/// </para>
/// </summary>
public sealed record ReservationStatusChangedDomainEvent : DomainEvent
{
    public required ListingReservationId ReservationId { get; init; }

    public required MarketplaceListingId ListingId { get; init; }

    public required SellerProfileId SellerId { get; init; }

    public required UserId BuyerUserId { get; init; }

    public required ReservationStatus PreviousStatus { get; init; }

    public required ReservationStatus CurrentStatus { get; init; }

    public required int Quantity { get; init; }

    public UserId? ChangedBy { get; init; }

    public string? Reason { get; init; }

    public required DateTimeOffset ChangedAt { get; init; }
}
