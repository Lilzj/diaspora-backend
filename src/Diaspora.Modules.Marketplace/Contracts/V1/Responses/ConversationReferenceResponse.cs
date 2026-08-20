namespace Diaspora.Modules.Marketplace.Contracts.V1.Responses;

/// <summary>
/// A pointer to a conversation about a listing.
/// <para>
/// <b>A reference, not a thread.</b> Marketplace owns no messages: it records that two members
/// want to talk about one item and hands out whatever handle a messaging provider issued.
/// <see cref="ExternalConversationId"/> is null on every deployment with no Communication module —
/// which is every deployment today — and the client falls back to
/// <see cref="ContactPreference"/>, which is exactly what the seller asked for.
/// </para>
/// </summary>
public sealed record ConversationReferenceResponse
{
    public required Guid ConversationId { get; init; }

    public required Guid ListingId { get; init; }

    public required string ListingTitle { get; init; }

    public Guid? ListingCoverMediaId { get; init; }

    public required Guid SellerId { get; init; }

    public required string SellerName { get; init; }

    public required Guid BuyerUserId { get; init; }

    /// <summary><c>Requested</c>, <c>Open</c> or <c>Closed</c>.</summary>
    public required string Status { get; init; }

    /// <summary>The messaging provider's handle. Null when no provider has claimed it.</summary>
    public string? ExternalConversationId { get; init; }

    /// <summary>Which provider claimed it.</summary>
    public string? Provider { get; init; }

    /// <summary>
    /// How the seller would rather be reached. What the client renders when no messaging provider
    /// is available.
    /// </summary>
    public required string ContactPreference { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public required DateTimeOffset LastActivityAt { get; init; }
}
