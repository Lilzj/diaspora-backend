using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Community.V1.Events;

/// <summary>
/// Somebody reacted.
/// <para>
/// Carries the reaction key and its sentiment as strings, not as an enum, because the reaction
/// catalogue is data. A consumer switching on a fixed set of reaction names silently stops
/// handling every kind a later deployment adds — which is exactly what a configurable catalogue
/// is meant to make possible.
/// </para>
/// </summary>
public sealed record CommunityReactionAddedIntegrationEvent : IntegrationEvent
{
    public required string ContentType { get; init; }

    public required Guid ContentId { get; init; }

    public required Guid UserId { get; init; }

    public required string ReactionKey { get; init; }

    /// <summary><c>positive</c>, <c>corroborating</c>, <c>disputing</c>, <c>negative</c>, <c>neutral</c>.</summary>
    public required string Sentiment { get; init; }

    public Guid? ContentAuthorId { get; init; }

    /// <summary>The country community the content belongs to, when it has one.</summary>
    public string? CountryCode { get; init; }
}
