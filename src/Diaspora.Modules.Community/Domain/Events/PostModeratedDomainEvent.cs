using Diaspora.Modules.Community.Domain.Enums;
using Diaspora.Modules.Community.Domain.Identifiers;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Community.Domain.Events;

/// <summary>
/// Raised when a decision on a Trust moderation case takes a post down or puts it back
/// (7 Nov 2026).
/// <para>
/// Deliberately not <see cref="PostDeletedDomainEvent"/>: a removal by a case is not a deletion,
/// and the deletion event takes back what the post earned. Consequences wait until the decision
/// stands. What reads this is the live alert map, which must stop drawing a post nobody may see.
/// </para>
/// </summary>
public sealed record PostModeratedDomainEvent : DomainEvent
{
    public required CommunityPostId PostId { get; init; }

    /// <summary>Where the post stands now: hidden, removed, or back up.</summary>
    public required PostStatus Status { get; init; }
}
