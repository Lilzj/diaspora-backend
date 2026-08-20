using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Marketplace.V1.Events;

/// <summary>
/// A listing was drafted.
/// <para>
/// <b>Published for audit and counting, and for nothing else.</b> A draft is not for sale: it has
/// no photographs yet, possibly no price, and only its author can see it. A consumer that indexed
/// this, or notified on it, would be surfacing something that does not exist to buy — every
/// discovery consumer keys on <c>ListingPublished</c> instead.
/// </para>
/// <para>
/// Consumers treat <c>EventId</c> as the idempotency key: delivery is at-least-once and unordered.
/// </para>
/// </summary>
public sealed record ListingCreatedIntegrationEvent : IntegrationEvent
{
    public required Guid ListingId { get; init; }

    public required Guid SellerId { get; init; }

    public required Guid CreatedBy { get; init; }

    public required string Title { get; init; }

    /// <summary>The deep-link handle. Stable across title edits.</summary>
    public required string Slug { get; init; }

    public required string CategoryKey { get; init; }

    /// <summary>ISO 3166-1 alpha-2, upper case.</summary>
    public required string CountryCode { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }
}
