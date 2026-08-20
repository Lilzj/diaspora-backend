using Diaspora.Modules.Community.Domain.ValueObjects;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Community.Domain.Events;

/// <summary>Raised when a member unsaves content.</summary>
public sealed record BookmarkRemovedDomainEvent : DomainEvent
{
    public required ContentReference Content { get; init; }

    public required UserId UserId { get; init; }
}
