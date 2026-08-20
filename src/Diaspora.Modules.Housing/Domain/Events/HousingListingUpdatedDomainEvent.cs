using Diaspora.Modules.Housing.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Housing.Domain.Events;

/// <summary>
/// Raised when a landlord changes a property's content.
/// <para>
/// <see cref="ChangedFields"/> is what makes this event useful rather than merely noisy: a rent
/// change is worth telling a watching member about, and a corrected typo is not. The list is the
/// aggregate's own field names, and consumers switch on it.
/// </para>
/// </summary>
public sealed record HousingListingUpdatedDomainEvent : DomainEvent
{
    public required HousingListingId ListingId { get; init; }

    public required LandlordProfileId LandlordId { get; init; }

    public UserId? ChangedBy { get; init; }

    public required IReadOnlyList<string> ChangedFields { get; init; }
}
