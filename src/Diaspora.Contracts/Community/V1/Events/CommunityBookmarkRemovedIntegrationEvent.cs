using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Community.V1.Events;

/// <summary>
/// Somebody took a save back.
/// <para>
/// The other half of <see cref="CommunityBookmarkAddedIntegrationEvent"/>. A module that shows
/// how many members saved its content (a guide's "saved" count) needs to hear both, or the
/// number only ever climbs.
/// </para>
/// </summary>
public sealed record CommunityBookmarkRemovedIntegrationEvent : IntegrationEvent
{
    public required string ContentType { get; init; }

    public required Guid ContentId { get; init; }

    public required Guid UserId { get; init; }
}
