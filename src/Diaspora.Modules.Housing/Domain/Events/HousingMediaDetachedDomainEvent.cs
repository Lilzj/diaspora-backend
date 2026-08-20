using Diaspora.Modules.Housing.Domain.Enums;
using Diaspora.Modules.Housing.Domain.Identifiers;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Housing.Domain.Events;

/// <summary>Raised when media is removed from a property.</summary>
public sealed record HousingMediaDetachedDomainEvent : DomainEvent
{
    public required HousingListingId ListingId { get; init; }

    public required LandlordProfileId LandlordId { get; init; }

    public required Guid MediaId { get; init; }

    public required HousingMediaRole Role { get; init; }
}
