using Diaspora.Modules.Housing.Domain.Identifiers;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Housing.Domain.Events;

/// <summary>
/// Raised when a property goes on the market.
/// <para>
/// <b>The module's most consequential fact.</b> It is what a search index, a saved search, a
/// neighbourhood feed and a relocation notification all key on, and the only listing event whose
/// published contract carries a full card payload - because every one of those consumers would
/// otherwise call straight back into Housing to get it.
/// </para>
/// <para>
/// <see cref="IsFirstPublication"/> distinguishes a new property from one returning to the market
/// after a tenancy. A consumer that notified on both identically would tell a member about a flat
/// they have already seen and already rejected.
/// </para>
/// </summary>
public sealed record HousingListingPublishedDomainEvent : DomainEvent
{
    public required HousingListingId ListingId { get; init; }

    public required LandlordProfileId LandlordId { get; init; }

    public required string Title { get; init; }

    public required string Slug { get; init; }

    public required string AccommodationTypeKey { get; init; }

    public required string CountryCode { get; init; }

    public Guid? NeighborhoodAreaId { get; init; }

    /// <summary>False when the property is coming back after being let. See the class summary.</summary>
    public required bool IsFirstPublication { get; init; }

    public required DateTimeOffset PublishedAt { get; init; }
}
