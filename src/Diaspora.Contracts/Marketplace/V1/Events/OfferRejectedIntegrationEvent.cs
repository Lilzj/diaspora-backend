using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Marketplace.V1.Events;

/// <summary>
/// A negotiation ended without agreement.
/// <para>
/// Published as well as the general settlement contract, and it is worth its own subscription for
/// one reason: a buyer whose offer was declined is the single best candidate for "here are similar
/// items", and no other event tells a discovery consumer that somebody wanted a specific thing and
/// did not get it.
/// </para>
/// <para>
/// <see cref="Status"/> distinguishes the three ways this happens — declined, taken back, or timed
/// out. Only the first is somebody saying no.
/// </para>
/// </summary>
public sealed record OfferRejectedIntegrationEvent : IntegrationEvent
{
    public required Guid OfferId { get; init; }

    public required Guid ListingId { get; init; }

    public required Guid SellerId { get; init; }

    public required Guid BuyerUserId { get; init; }

    /// <summary><c>Rejected</c>, <c>Withdrawn</c> or <c>Expired</c>.</summary>
    public required string Status { get; init; }

    /// <summary>The amount that stood when it ended. What "they turned down X" means.</summary>
    public decimal? Amount { get; init; }

    public required string CurrencyCode { get; init; }

    /// <summary>Who ended it. Null on an expiry, which the clock ended.</summary>
    public Guid? SettledBy { get; init; }

    public string? Reason { get; init; }

    public required DateTimeOffset SettledAt { get; init; }
}
