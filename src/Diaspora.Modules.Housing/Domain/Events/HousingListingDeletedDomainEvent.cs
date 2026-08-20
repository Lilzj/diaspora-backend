using Diaspora.Modules.Housing.Domain.Identifiers;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Housing.Domain.Events;

/// <summary>Raised when a property is soft-deleted. Consumers de-index on it.</summary>
public sealed record HousingListingDeletedDomainEvent : DomainEvent
{
    public required HousingListingId ListingId { get; init; }

    public required LandlordProfileId LandlordId { get; init; }

    public required DateTimeOffset DeletedAt { get; init; }
}
