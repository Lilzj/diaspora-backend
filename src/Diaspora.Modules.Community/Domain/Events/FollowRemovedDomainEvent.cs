using Diaspora.Modules.Community.Domain.Enums;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Community.Domain.Events;

/// <summary>
/// Raised when a member unfollows.
/// <para>
/// Published, and deliberately not notified. Telling somebody they have been unfollowed is a
/// product decision this module does not make for anybody, but a following feed cache that
/// never hears about it keeps serving content the member has explicitly opted out of.
/// </para>
/// </summary>
public sealed record FollowRemovedDomainEvent : DomainEvent
{
    public required UserId FollowerId { get; init; }

    public required FollowTargetType TargetType { get; init; }

    public required Guid TargetId { get; init; }
}
