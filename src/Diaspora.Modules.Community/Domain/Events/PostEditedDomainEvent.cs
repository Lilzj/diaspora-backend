using Diaspora.Modules.Community.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Community.Domain.Events;

/// <summary>
/// Raised when a published post body changes.
/// <para>
/// Carries the revision number so a consumer can tell an edit it has already seen from one it
/// has not. Search re-indexes on this; a notification module deliberately does not, because
/// notifying every reader that a typo was fixed is how a feed teaches members to mute it.
/// </para>
/// </summary>
public sealed record PostEditedDomainEvent : DomainEvent
{
    public required CommunityPostId PostId { get; init; }

    public required UserId EditedBy { get; init; }

    public required int RevisionNumber { get; init; }

    /// <summary>True when the edit happened after the silent-edit window closed.</summary>
    public required bool IsMarkedEdited { get; init; }
}
