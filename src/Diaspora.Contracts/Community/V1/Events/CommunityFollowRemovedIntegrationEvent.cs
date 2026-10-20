using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Community.V1.Events;

/// <summary>
/// Somebody stopped following a member, an organization or a hashtag.
/// <para>
/// The mirror of <see cref="CommunityFollowCreatedIntegrationEvent"/>, with the same fields, so a
/// consumer that keeps a follower tally can take one away as well as add one. Until 20 Oct 2026 the
/// removal was raised as a domain event and never published, so every tally kept outside Community
/// only ever went up.
/// </para>
/// </summary>
public sealed record CommunityFollowRemovedIntegrationEvent : IntegrationEvent
{
    public required Guid FollowerId { get; init; }

    /// <summary><c>member</c>, <c>organization</c> or <c>hashtag</c>.</summary>
    public required string TargetType { get; init; }

    public required Guid TargetId { get; init; }
}
