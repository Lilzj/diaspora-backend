using Diaspora.Modules.Community.Domain.ValueObjects;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Community.Domain.Events;

/// <summary>Raised when a member withdraws a reaction.</summary>
public sealed record ReactionRemovedDomainEvent : DomainEvent
{
    public required ContentReference Content { get; init; }

    public required UserId UserId { get; init; }

    public required string ReactionKey { get; init; }
}
