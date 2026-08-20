namespace Diaspora.Modules.Housing.Contracts.V1.Requests;

/// <summary>
/// Drafts a property.
/// <para>
/// <b>Always lands as a private draft, whatever the client sends.</b> Publishing is a separate,
/// deliberate act with its own permission and its own checks — a create that could publish would
/// let a client skip every one of them.
/// </para>
/// <para>
/// <b>The address is optional and its visibility defaults to neighbourhood-only.</b> A landlord who
/// sends a street line is choosing to store it, not to publish it; what a browser sees is decided
/// by <see cref="AddressVisibility"/> and by whether they hold a confirmed viewing.
/// </para>
/// </summary>
public sealed record CreateHousingListingRequest
{
    // ── Content ─────────────────────────────────────────────────────────────────────────────

    public required string Title { get; init; }

    /// <summary>The one-paragraph pitch. Required, and what every card renders.</summary>
    public required string Summary { get; init; }

    public string? Description { get; init; }

    /// <summary>The accommodation-type catalogue key.</summary>
    public required string AccommodationTypeKey { get; init; }

    // ── Money ───────────────────────────────────────────────────────────────────────────────

    /// <summary>The rent. Null means price on application, which is a real state rather than zero.</summary>
    public decimal? RentAmount { get; init; }

    /// <summary>ISO 4217. Required whenever a rent is given; nothing converts it.</summary>
    public string? CurrencyCode { get; init; }

    /// <summary>PerNight, PerWeek, PerMonth, PerQuarter, PerYear, PerStay. Never normalised.</summary>
    public string? RentPeriod { get; init; }

    /// <summary>
    /// The deposit, in the same currency as the rent. Part of the price rather than a footnote:
    /// it is how a member discovers they cannot afford somewhere they had arranged to view.
    /// </summary>
    public decimal? DepositAmount { get; init; }

    public bool IsNegotiable { get; init; }

    public bool UtilitiesIncluded { get; init; }

    public bool InternetIncluded { get; init; }

    // ── Property ────────────────────────────────────────────────────────────────────────────

    /// <summary>Zero for a studio, which is a real answer rather than a missing one.</summary>
    public int Bedrooms { get; init; }

    /// <summary>Decimal, so a half-bathroom survives.</summary>
    public decimal Bathrooms { get; init; }

    public int? MaximumOccupants { get; init; }

    public int? FloorAreaSquareMetres { get; init; }

    public string? LeaseType { get; init; }

    public string? FurnishedStatus { get; init; }

    public string? Parking { get; init; }

    public string? SmokingPolicy { get; init; }

    public string? PetPolicy { get; init; }

    // ── Availability ────────────────────────────────────────────────────────────────────────

    /// <summary>When it can be moved into. Null means now.</summary>
    public DateTimeOffset? AvailableFrom { get; init; }

    /// <summary>When it stops being available. Null means open-ended.</summary>
    public DateTimeOffset? AvailableUntil { get; init; }

    /// <summary>Identical units on one advert: hostel beds, or flats in one block. One by default.</summary>
    public int TotalUnits { get; init; } = 1;

    public int? MinimumStayNights { get; init; }

    public int? MaximumStayNights { get; init; }

    // ── Who it is for ───────────────────────────────────────────────────────────────────────

    public bool AcceptsShortStay { get; init; }

    public bool AcceptsLongStay { get; init; } = true;

    public bool IsStudentFriendly { get; init; }

    public bool IsFamilyFriendly { get; init; }

    /// <summary>Whether somebody else lives there. Gates roommate preferences and gender preference.</summary>
    public bool IsSharedAccommodation { get; init; }

    public bool IsLookingForRoommate { get; init; }

    /// <summary>Whether this landlord asks for a viewing before a reservation.</summary>
    public bool RequiresViewingBeforeReservation { get; init; }

    // ── Where ───────────────────────────────────────────────────────────────────────────────

    public required string CountryCode { get; init; }

    public string? RegionName { get; init; }

    public string? CityName { get; init; }

    /// <summary>
    /// B4's administrative area at neighbourhood level. Resolved against Location before it is
    /// stored, so a property is placed in a real neighbourhood rather than a typed one.
    /// </summary>
    public Guid? NeighborhoodAreaId { get; init; }

    public string? AddressLine1 { get; init; }

    public string? AddressLine2 { get; init; }

    public string? PostalCode { get; init; }

    public double? Latitude { get; init; }

    public double? Longitude { get; init; }

    /// <summary>
    /// NeighborhoodOnly, StreetOnly, OnViewingConfirmed, AuthenticatedMembers or Public. Defaults
    /// to the safest value when omitted.
    /// </summary>
    public string? AddressVisibility { get; init; }

    // ── Collections ─────────────────────────────────────────────────────────────────────────

    public IReadOnlyList<HousingAmenityRequest>? Amenities { get; init; }

    public IReadOnlyList<HouseRuleRequest>? HouseRules { get; init; }

    public IReadOnlyList<HousingMediaRequest>? Media { get; init; }

    /// <summary>B7 providers connected to the property: the managing agent, the relocation partner.</summary>
    public IReadOnlyList<Guid>? ProviderIds { get; init; }
}
