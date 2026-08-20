using Diaspora.Modules.Community.Domain.Enums;
using Diaspora.Modules.Community.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Community.Domain.Events;

/// <summary>Raised when a member follows another member, an organization or a hashtag.</summary>
public sealed record FollowCreatedDomainEvent : DomainEvent
{
    public required FollowId FollowId { get; init; }

    public required UserId FollowerId { get; init; }

    public required FollowTargetType TargetType { get; init; }

    public required Guid TargetId { get; init; }
}
