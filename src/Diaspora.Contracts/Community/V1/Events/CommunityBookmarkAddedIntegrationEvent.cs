using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Community.V1.Events;

/// <summary>
/// Somebody saved something.
/// <para>
/// The strongest intent signal a member gives without writing anything, and the reason this is
/// an integration event rather than an internal one: a recommendation engine that only sees
/// reactions sees applause, while a save means somebody means to come back.
/// </para>
/// </summary>
public sealed record CommunityBookmarkAddedIntegrationEvent : IntegrationEvent
{
    public required string ContentType { get; init; }

    public required Guid ContentId { get; init; }

    public required Guid UserId { get; init; }

    public Guid? ContentAuthorId { get; init; }
}
