using Diaspora.Modules.Marketplace.Domain.Enums;
using Diaspora.Modules.Marketplace.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Marketplace.Domain.Events;

/// <summary>
/// Raised on every lifecycle transition.
/// <para>
/// <b>One event rather than seven, and the relay decides which published contract each transition
/// corresponds to.</b> That is what keeps the domain from learning what the wire contracts are
/// called: "moved to Sold" becomes <c>ListingSold</c> in one place, and an aggregate that raised
/// <c>ListingSoldIntegrationEvent</c> directly would be an aggregate a contract change has to
/// edit.
/// </para>
/// </summary>
public sealed record ListingStatusChangedDomainEvent : DomainEvent
{
    public required MarketplaceListingId ListingId { get; init; }

    public required SellerProfileId SellerId { get; init; }

    public required ListingStatus PreviousStatus { get; init; }

    public required ListingStatus CurrentStatus { get; init; }

    public UserId? ChangedBy { get; init; }

    public string? Reason { get; init; }

    public required DateTimeOffset ChangedAt { get; init; }
}
