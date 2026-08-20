using Diaspora.Modules.Housing.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Housing.Domain.Events;

/// <summary>
/// Raised when a property is drafted, visible to nobody but its landlord.
/// <para>
/// Separate from <see cref="HousingListingPublishedDomainEvent"/> deliberately: a creation is
/// worth auditing and counting, and emphatically not worth indexing, notifying on or putting in a
/// feed. A draft is not on the market yet, and a member whose saved search fired on one would be
/// enquiring about a property that does not exist. Every downstream consumer keys on publication.
/// </para>
/// </summary>
public sealed record HousingListingCreatedDomainEvent : DomainEvent
{
    public required HousingListingId ListingId { get; init; }

    public required LandlordProfileId LandlordId { get; init; }

    public required UserId CreatedBy { get; init; }

    public required string Title { get; init; }

    public required string Slug { get; init; }

    public required string AccommodationTypeKey { get; init; }

    public required string CountryCode { get; init; }

    /// <summary>B4's area, when the landlord placed the property in a neighbourhood.</summary>
    public Guid? NeighborhoodAreaId { get; init; }
}
