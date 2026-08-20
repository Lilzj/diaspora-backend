using Diaspora.Modules.Marketplace.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Marketplace.Domain.Events;

/// <summary>
/// Raised when a buyer asks to talk to a seller about a listing.
/// <para>
/// <b>This is the whole of Marketplace's involvement in messaging.</b> The module records that
/// two members want to talk about one item and hands out a reference; a future Communication
/// module claims that reference and owns everything after it. Nothing here delivers a message,
/// stores a message, or knows whether one was ever sent.
/// </para>
/// </summary>
public sealed record ConversationRequestedDomainEvent : DomainEvent
{
    public required ListingConversationId ConversationId { get; init; }

    public required MarketplaceListingId ListingId { get; init; }

    public required SellerProfileId SellerId { get; init; }

    public required UserId BuyerUserId { get; init; }

    public required UserId SellerUserId { get; init; }

    public required DateTimeOffset RequestedAt { get; init; }
}
