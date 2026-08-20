using Diaspora.Modules.Housing.Domain.Enums;
using Diaspora.Modules.Housing.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Housing.Domain.Events;

/// <summary>Raised when a moderator hides, flags, suspends or clears a property.</summary>
public sealed record HousingModerationChangedDomainEvent : DomainEvent
{
    public required HousingListingId ListingId { get; init; }

    public required LandlordProfileId LandlordId { get; init; }

    public required HousingModerationState PreviousState { get; init; }

    public required HousingModerationState CurrentState { get; init; }

    public UserId? ChangedBy { get; init; }

    public string? Reason { get; init; }
}
