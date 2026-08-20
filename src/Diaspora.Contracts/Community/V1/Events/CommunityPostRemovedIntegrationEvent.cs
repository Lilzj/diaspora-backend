using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Community.V1.Events;

/// <summary>
/// A post left circulation.
/// <para>
/// Covers author deletion, moderator removal and expiry, distinguished by <see cref="Reason"/>.
/// A search index must drop the document on the first two and may keep it on the third — an
/// expired post is still readable by permalink, and dropping it makes an existing link 404.
/// </para>
/// </summary>
public sealed record CommunityPostRemovedIntegrationEvent : IntegrationEvent
{
    public required Guid PostId { get; init; }

    public required Guid AuthorId { get; init; }

    /// <summary><c>deleted</c>, <c>removed</c> or <c>expired</c>.</summary>
    public required string Reason { get; init; }
}
