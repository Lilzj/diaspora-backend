namespace Diaspora.Modules.Marketplace.Extensibility.Conversations;

/// <summary>
/// What a messaging provider is told when a buyer wants to talk to a seller.
/// <para>
/// Flat primitives, and enough context for the provider to title the thread without calling back
/// into this module. It carries no message body: the opening note stays on the reference, and a
/// provider that wants it asks for it — a marketplace that pushed message content into an
/// integration contract would be a marketplace that owns messages.
/// </para>
/// </summary>
public sealed record MarketplaceConversationRequest
{
    public required Guid ConversationId { get; init; }

    public required Guid ListingId { get; init; }

    /// <summary>The listing's title, so the thread has a name without a second read.</summary>
    public required string ListingTitle { get; init; }

    public required Guid BuyerUserId { get; init; }

    public required Guid SellerUserId { get; init; }

    public required Guid SellerId { get; init; }

    /// <summary>The seller's trading name, for the thread header.</summary>
    public required string SellerName { get; init; }
}
