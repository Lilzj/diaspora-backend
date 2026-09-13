using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Guides.V1.Events;

/// <summary>
/// A reader shared a guide — reported by the client through <c>POST /guides/{guideId}/share</c>.
/// <para>
/// <b>Why this exists.</b> Until 13 Sep 2026 a share only bumped the guide's own counter, so the
/// platform's analytics never learned of it: a creator's "Shares" read zero however often their
/// guides were shared. Analytics counts shares from Community's share stream, and a guide shared
/// from its own screen never reaches that stream — so this is not a second copy of it, and a
/// consumer that listens to both counts each share once.
/// </para>
/// <para>
/// One message per share is proportionate where one per view would not be: a share is an
/// act a member chose to take, not a counter tick.
/// </para>
/// </summary>
public sealed record GuideSharedIntegrationEvent : IntegrationEvent
{
    public required Guid GuideId { get; init; }

    /// <summary>
    /// The member who wrote the guide — whose share it is to count. Nullable, as on every guide
    /// contract, though the publisher holds the guide and always sets it; a consumer without it
    /// attributes the share through the guide's publication.
    /// </summary>
    public Guid? AuthorUserId { get; init; }

    /// <summary>The member who shared it. Null when the reader was not signed in.</summary>
    public Guid? SharedBy { get; init; }

    public required DateTimeOffset SharedAt { get; init; }
}
