using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Marketplace.V1.Events;

/// <summary>
/// A buyer opened a negotiation.
/// <para>
/// Carries the amount and the listing's title, because the one consumer that matters — the
/// seller's notification — is useless without both. It deliberately carries nothing about the
/// buyer beyond their identifier: an offer is personal data, and the bus is not the place for a
/// name, a message or a history.
/// </para>
/// </summary>
public sealed record OfferCreatedIntegrationEvent : IntegrationEvent
{
    public required Guid OfferId { get; init; }

    public required Guid ListingId { get; init; }

    public required string ListingTitle { get; init; }

    public required Guid SellerId { get; init; }

    /// <summary>The member behind the seller profile, for a notification to reach.</summary>
    public required Guid SellerUserId { get; init; }

    public required Guid BuyerUserId { get; init; }

    public required decimal Amount { get; init; }

    /// <summary>ISO 4217, upper case. Always the listing's currency.</summary>
    public required string CurrencyCode { get; init; }

    public required int Quantity { get; init; }

    /// <summary>The seller's asking price, so a consumer can render the gap without a second read.</summary>
    public decimal? AskingPrice { get; init; }

    public DateTimeOffset? ExpiresAt { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }
}
