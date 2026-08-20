using Diaspora.Modules.Housing.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Housing.Domain.Events;

/// <summary>Raised when a landlord profile's own details change.</summary>
public sealed record LandlordUpdatedDomainEvent : DomainEvent
{
    public required LandlordProfileId LandlordId { get; init; }

    public required UserId OwnerUserId { get; init; }

    public required IReadOnlyList<string> ChangedFields { get; init; }
}
