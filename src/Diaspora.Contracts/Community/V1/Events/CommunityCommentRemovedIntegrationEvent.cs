using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Community.V1.Events;

/// <summary>A comment stopped being readable. The row survives, tombstoned, in the thread.</summary>
public sealed record CommunityCommentRemovedIntegrationEvent : IntegrationEvent
{
    public required Guid CommentId { get; init; }

    public required string ContentType { get; init; }

    public required Guid ContentId { get; init; }

    public required bool ByModerator { get; init; }
}
