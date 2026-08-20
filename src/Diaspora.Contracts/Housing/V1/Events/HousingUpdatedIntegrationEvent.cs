using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Housing.V1.Events;

/// <summary>
/// A property's content changed — B23.x Wave 12.
/// <para>
/// <b>B11 raised <c>HousingListingUpdatedDomainEvent</c> from the first release and never published
/// it.</b> The domain event's own summary says why the list matters — "a rent change is worth telling
/// a watching member about, and a corrected typo is not" — and then nothing outside the module could
/// act on either, because no contract carried it. Marketplace published the equivalent from B10;
/// Housing did not, which is why "the price changes on something I am watching" worked for a sofa and
/// not for a flat.
/// </para>
/// <para>
/// <b>The rent travels with the event when the rent is what moved</b>, for the reason
/// <c>ListingUpdatedIntegrationEvent</c> gives about price: making every consumer read the listing
/// back to find out what it now costs would put the whole watchlist fan-out through this module on
/// every edit.
/// </para>
/// <para>
/// <b>It carries no address, and that is not an omission.</b> A property's exact address is disclosed
/// by a request with a viewer identity attached, never by a payload on a bus — the rule
/// <c>HousingPublishedIntegrationEvent</c> and <c>ViewingScheduledIntegrationEvent</c> both keep.
/// </para>
/// </summary>
public sealed record HousingUpdatedIntegrationEvent : IntegrationEvent
{
    public required Guid ListingId { get; init; }

    public required Guid LandlordId { get; init; }

    public Guid? ChangedBy { get; init; }

    /// <summary>
    /// The aggregate's own field names — <c>Title</c>, <c>Description</c>, <c>Price</c>,
    /// <c>Availability</c>, <c>Amenities</c>, <c>Location</c>, and so on.
    /// <para>
    /// Published as the aggregate names them rather than lower-cased to match Marketplace's, because
    /// the domain event has always carried them in this form and re-casing on the way out would make
    /// the published list disagree with the one B11's own handlers read. Consumers compare
    /// case-insensitively; the platform's automation consumer does.
    /// </para>
    /// </summary>
    public required IReadOnlyList<string> ChangedFields { get; init; }

    /// <summary>The rent as it now stands, when <c>Price</c> is among the changed fields and disclosed.</summary>
    public decimal? RentAmount { get; init; }

    /// <summary>The rent before the change, where the module knew it, so a consumer can say "reduced from".</summary>
    public decimal? PreviousRentAmount { get; init; }

    public string? CurrencyCode { get; init; }

    /// <summary><c>Monthly</c>, <c>Weekly</c> and so on. Null when the listing publishes no period.</summary>
    public string? RentPeriod { get; init; }

    /// <summary>
    /// False when the landlord publishes no figure.
    /// <para>
    /// A price condition must never be compared against an undisclosed rent — that would be the
    /// platform reporting a number the landlord chose not to publish — so the flag travels with the
    /// amount rather than being inferred from it being non-null.
    /// </para>
    /// </summary>
    public required bool IsRentDisclosed { get; init; }

    public string? CountryCode { get; init; }

    /// <summary>The B4 area, where the listing names one. Never the address.</summary>
    public Guid? NeighborhoodAreaId { get; init; }

    public required DateTimeOffset ChangedAt { get; init; }
}
