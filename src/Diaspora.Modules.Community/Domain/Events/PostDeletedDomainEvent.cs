using Diaspora.Modules.Community.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Community.Domain.Events;

/// <summary>
/// Raised when a post leaves circulation, whether its author deleted it or a moderator removed
/// it. Consumers must treat both the same way: stop showing it.
/// </summary>
public sealed record PostDeletedDomainEvent : DomainEvent
{
    public required CommunityPostId PostId { get; init; }

    public required UserId AuthorId { get; init; }

    /// <summary>True when a moderator removed it rather than the author deleting it.</summary>
    public required bool ByModerator { get; init; }
}
