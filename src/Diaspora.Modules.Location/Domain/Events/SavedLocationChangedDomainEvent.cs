using Diaspora.Modules.Location.Domain.Profiles;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Location.Domain.Events;

/// <summary>Raised when a member saves or removes a labelled place.</summary>
public sealed record SavedLocationChangedDomainEvent : DomainEvent
{
    public required LocationProfileId ProfileId { get; init; }

    public required UserId UserId { get; init; }

    public required string Label { get; init; }

    public required bool Removed { get; init; }
}
