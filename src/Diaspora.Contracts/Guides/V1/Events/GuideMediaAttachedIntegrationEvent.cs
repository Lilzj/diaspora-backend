using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Guides.V1.Events;

/// <summary>
/// A media item was attached to a guide.
/// <para>
/// Carries the media identifier and never a URL — ADR-005's rule holds at module boundaries too.
/// A consumer wanting bytes or a rendition resolves <see cref="MediaId"/> through the Media
/// module, which stays free to move storage, re-encode or expire URLs without a single stale
/// link surviving in somebody's projection.
/// </para>
/// </summary>
public sealed record GuideMediaAttachedIntegrationEvent : IntegrationEvent
{
    public required Guid GuideId { get; init; }

    public required Guid MediaId { get; init; }

    /// <summary>The <c>GuideMediaRole</c> name — <c>Hero</c>, <c>Cover</c>, <c>Gallery</c>, <c>Attachment</c>, <c>Embedded</c>.</summary>
    public required string Role { get; init; }

    public required DateTimeOffset AttachedAt { get; init; }
}
