using Diaspora.Modules.Housing.Domain.Enums;
using Diaspora.Modules.Housing.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Housing.Domain.Events;

/// <summary>Raised when a member sets up a landlord profile.</summary>
public sealed record LandlordRegisteredDomainEvent : DomainEvent
{
    public required LandlordProfileId LandlordId { get; init; }

    public required UserId OwnerUserId { get; init; }

    public required string DisplayName { get; init; }

    public required LandlordKind Kind { get; init; }

    /// <summary>The B7 provider behind an agency or a hostel, when there is one.</summary>
    public Guid? ProviderId { get; init; }
}
