using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Community.V1.Events;

/// <summary>Somebody followed a member, an organization or a hashtag.</summary>
public sealed record CommunityFollowCreatedIntegrationEvent : IntegrationEvent
{
    public required Guid FollowerId { get; init; }

    /// <summary><c>member</c>, <c>organization</c> or <c>hashtag</c>.</summary>
    public required string TargetType { get; init; }

    public required Guid TargetId { get; init; }
}
