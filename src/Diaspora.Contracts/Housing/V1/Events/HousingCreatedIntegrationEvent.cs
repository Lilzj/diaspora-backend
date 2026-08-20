using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Housing.V1.Events;

/// <summary>
/// A property was drafted.
/// <para>
/// <b>Published for audit and counting, and for nothing else.</b> A draft is not on the market: it
/// may have no photographs, no rent and no available date, and only its landlord can see it. A
/// consumer that indexed this, or notified on it, would surface a home nobody can actually take -
/// every discovery consumer keys on <c>HousingPublished</c> instead.
/// </para>
/// <para>
/// Consumers treat <c>EventId</c> as the idempotency key: delivery is at-least-once and unordered.
/// </para>
/// </summary>
public sealed record HousingCreatedIntegrationEvent : IntegrationEvent
{
    public required Guid ListingId { get; init; }

    public required Guid LandlordId { get; init; }

    public required Guid CreatedBy { get; init; }

    public required string Title { get; init; }

    /// <summary>The deep-link handle. Stable across title edits.</summary>
    public required string Slug { get; init; }

    public required string AccommodationTypeKey { get; init; }

    /// <summary>ISO 3166-1 alpha-2, upper case.</summary>
    public required string CountryCode { get; init; }

    /// <summary>
    /// The Location administrative area at neighbourhood level, where the landlord placed one.
    /// Carried so a consumer can join to B4 without joining to Housing.
    /// </summary>
    public Guid? NeighborhoodAreaId { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }
}
