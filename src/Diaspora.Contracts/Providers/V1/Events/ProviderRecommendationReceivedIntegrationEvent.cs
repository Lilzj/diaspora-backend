using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Providers.V1.Events;

/// <summary>A member recommended, endorsed or wrote a testimonial for a provider.</summary>
public sealed record ProviderRecommendationReceivedIntegrationEvent : IntegrationEvent
{
    public required Guid RecommendationId { get; init; }

    public required Guid ProviderId { get; init; }

    public required Guid RecommenderId { get; init; }

    /// <summary>The <c>RecommendationKind</c> name — <c>Recommend</c>, <c>Testimonial</c>, <c>Endorsement</c>.</summary>
    public required string Kind { get; init; }

    public required DateTimeOffset ReceivedAt { get; init; }

    /// <summary>
    /// The provider's notifiable member — B13.1. Its current primary owner, and the person a
    /// recommendation is actually about.
    /// <para>Null on events published before B13.1, and null when the provider could not be read.</para>
    /// </summary>
    public Guid? OwnerUserId { get; init; }
}
