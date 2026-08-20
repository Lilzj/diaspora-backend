using Diaspora.Modules.Housing.Domain.Enums;
using Diaspora.Modules.Housing.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Housing.Domain.Events;

/// <summary>Raised when a landlord is suspended, reinstated or closes their profile.</summary>
public sealed record LandlordStatusChangedDomainEvent : DomainEvent
{
    public required LandlordProfileId LandlordId { get; init; }

    public required UserId OwnerUserId { get; init; }

    public required LandlordStatus PreviousStatus { get; init; }

    public required LandlordStatus CurrentStatus { get; init; }

    public UserId? ChangedBy { get; init; }

    public string? Reason { get; init; }
}
