using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Housing.V1.Events;

/// <summary>
/// A property went on the market.
/// <para>
/// <b>The module most consequential contract.</b> A search index, a saved search, a neighbourhood
/// feed and a relocation notification all key on it, which is why it carries a full card payload
/// rather than an identifier: every one of those consumers would otherwise call straight back into
/// Housing to render one line.
/// </para>
/// <para>
/// <b>No address, at any disclosure level.</b> The bus is the last place a home street name
/// belongs, and a consumer that needs one asks Housing with a viewer identity so the disclosure
/// rules apply. The neighbourhood and the city are the finest geography this contract carries.
/// </para>
/// <para>
/// <see cref="IsFirstPublication"/> distinguishes a new property from one returning to the market
/// after a tenancy. A consumer that notified on both identically would tell a member about a home
/// they have already seen and already rejected.
/// </para>
/// </summary>
public sealed record HousingPublishedIntegrationEvent : IntegrationEvent
{
    public required Guid ListingId { get; init; }

    public required Guid LandlordId { get; init; }

    public required string LandlordName { get; init; }

    /// <summary>Individual, EstateAgent, PropertyManager, Hostel and the rest.</summary>
    public required string LandlordKind { get; init; }

    public required bool IsLandlordVerified { get; init; }

    public required string Title { get; init; }

    public required string Slug { get; init; }

    public string? Summary { get; init; }

    public required string AccommodationTypeKey { get; init; }

    /// <summary>False when the rent is on application, which is a real state rather than zero.</summary>
    public required bool IsRentDisclosed { get; init; }

    public decimal? RentAmount { get; init; }

    /// <summary>ISO 4217. Nothing in the platform converts it.</summary>
    public string? CurrencyCode { get; init; }

    /// <summary>PerNight, PerWeek, PerMonth and the rest. Nothing normalises it.</summary>
    public string? RentPeriod { get; init; }

    public decimal? DepositAmount { get; init; }

    public required int Bedrooms { get; init; }

    public required decimal Bathrooms { get; init; }

    public required string FurnishedStatus { get; init; }

    /// <summary>Units advertised on this one listing. More than one for hostel beds.</summary>
    public required int TotalUnits { get; init; }

    public DateTimeOffset? AvailableFrom { get; init; }

    public required bool AcceptsShortStay { get; init; }

    public required bool AcceptsLongStay { get; init; }

    public required bool IsStudentFriendly { get; init; }

    public required bool IsFamilyFriendly { get; init; }

    public required bool IsSharedAccommodation { get; init; }

    public required string CountryCode { get; init; }

    public string? RegionName { get; init; }

    public string? CityName { get; init; }

    public string? NeighborhoodName { get; init; }

    /// <summary>The Location area. The join key every consumer already shares.</summary>
    public Guid? NeighborhoodAreaId { get; init; }

    /// <summary>A Media identifier. Never a URL.</summary>
    public Guid? CoverMediaId { get; init; }

    /// <summary>Amenity catalogue keys. What a facet or a saved search matches on.</summary>
    public IReadOnlyList<string> AmenityKeys { get; init; } = [];

    /// <summary>False when the property is coming back after being let. See the class summary.</summary>
    public required bool IsFirstPublication { get; init; }

    public required DateTimeOffset PublishedAt { get; init; }

    /// <summary>
    /// The member behind the landlord profile.
    /// <para>
    /// <b>The field an attribution consumer cannot do its job without.</b> This contract named only
    /// <see cref="LandlordId"/> — the <em>profile</em> — so a consumer that wanted to credit the
    /// person who listed the property had a profile identifier and no way to reach them.
    /// <c>ListingPublishedIntegrationEvent.SellerUserId</c> was added for exactly this reason in
    /// Wave 12 and states it in the same words; this is the same field on the same argument, and its
    /// absence here is BG-10.
    /// </para>
    /// <para>
    /// <b>Attribution, not delivery.</b> It is <c>LandlordProfile.OwnerUserId</c> — the member who
    /// registered the profile. A consumer that needs to <em>reach</em> a landlord asks Housing with
    /// a viewer identity so the contact-disclosure rules apply; this field credits, it does not
    /// address.
    /// </para>
    /// <para>
    /// <b>Costs nothing to publish.</b> The relay already reads the landlord aggregate back to fill
    /// in <see cref="LandlordName"/>, <see cref="LandlordKind"/> and
    /// <see cref="IsLandlordVerified"/>, so there is no new read.
    /// </para>
    /// <para>
    /// <b>Appended, never inserted.</b> Property order is part of the serialised shape, and a field
    /// added in the middle changes how every previously-written outbox row deserialises. Null on
    /// events published before this change, and null when the landlord could not be read.
    /// </para>
    /// </summary>
    public Guid? LandlordUserId { get; init; }
}
