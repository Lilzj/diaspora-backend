using Diaspora.Modules.Location.Domain.Profiles;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Location.Domain.Events;

/// <summary>Raised the first time a member acquires a geographic context.</summary>
public sealed record LocationProfileCreatedDomainEvent : DomainEvent
{
    public required LocationProfileId ProfileId { get; init; }

    public required UserId UserId { get; init; }
}
