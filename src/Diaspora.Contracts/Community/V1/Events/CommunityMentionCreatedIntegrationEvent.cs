using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Community.V1.Events;

/// <summary>
/// Somebody was mentioned.
/// <para>
/// Only ever published for a mention that resolved to a real target. An unresolved handle is
/// stored so the body renders faithfully, but there is nobody to notify.
/// </para>
/// </summary>
public sealed record CommunityMentionCreatedIntegrationEvent : IntegrationEvent
{
    public required Guid MentionId { get; init; }

    public required string ContentType { get; init; }

    public required Guid ContentId { get; init; }

    public required Guid MentionedBy { get; init; }

    /// <summary><c>member</c>, <c>organization</c>, <c>provider</c>.</summary>
    public required string TargetType { get; init; }

    public required Guid TargetId { get; init; }
}
