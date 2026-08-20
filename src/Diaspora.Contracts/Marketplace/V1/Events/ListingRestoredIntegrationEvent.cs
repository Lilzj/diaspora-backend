using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Marketplace.V1.Events;

/// <summary>
/// A listing came back — from the archive, or from a soft deletion.
/// <para>
/// <b>The counterpart every de-indexing consumer needs.</b> Without it a restored listing is one
/// the seller can see on their own dashboard and no buyer can ever find, which is the most
/// confusing failure a marketplace can present: the seller is certain the item is listed, and every
/// search says it is not.
/// </para>
/// <para>
/// <b>It does not say the listing is available.</b> A restored listing returns as a draft, and a
/// consumer that put it straight back into a discovery surface would be advertising something its
/// seller has not re-published. <c>ListingPublished</c> follows if and when they do.
/// </para>
/// </summary>
public sealed record ListingRestoredIntegrationEvent : IntegrationEvent
{
    public required Guid ListingId { get; init; }

    public required Guid SellerId { get; init; }

    /// <summary>True when it came back from soft deletion, false when from the archive.</summary>
    public required bool WasDeleted { get; init; }

    public Guid? RestoredBy { get; init; }

    public required DateTimeOffset RestoredAt { get; init; }
}
