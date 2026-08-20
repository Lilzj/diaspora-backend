using Diaspora.Modules.Housing.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Housing.Domain.Events;

/// <summary>Raised when a member asks to see a property.</summary>
public sealed record HousingViewingRequestedDomainEvent : DomainEvent
{
    public required HousingViewingId ViewingId { get; init; }

    public required HousingListingId ListingId { get; init; }

    public required LandlordProfileId LandlordId { get; init; }

    public required UserId SeekerUserId { get; init; }

    public DateTimeOffset? PreferredAt { get; init; }

    public required bool IsVirtual { get; init; }

    public required DateTimeOffset RequestedAt { get; init; }
}
