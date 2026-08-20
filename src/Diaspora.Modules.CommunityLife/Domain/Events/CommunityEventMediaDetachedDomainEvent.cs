using Diaspora.Modules.CommunityLife.Domain.Enums;
using Diaspora.Modules.CommunityLife.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.CommunityLife.Domain.Events;

/// <summary>Raised when a Media reference is removed from an event.</summary>
public sealed record CommunityEventMediaDetachedDomainEvent : DomainEvent
{
    public required CommunityEventId CommunityEventId { get; init; }

    public required Guid MediaId { get; init; }

    public required CommunityLifeMediaRole Role { get; init; }
}
