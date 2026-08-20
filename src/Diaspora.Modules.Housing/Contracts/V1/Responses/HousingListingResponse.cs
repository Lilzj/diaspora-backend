namespace Diaspora.Modules.Housing.Contracts.V1.Responses;

/// <summary>
/// A property, in full.
/// <para>
/// <b>What a viewer is entitled to, not what is stored.</b> The address is the redacted copy, the
/// landlord contact details are the redacted copy, and both were produced by asking the value
/// object who is looking rather than by a mapper remembering a rule.
/// </para>
/// </summary>
public sealed record HousingListingResponse
{
    public required Guid Id { get; init; }

    public required string Slug { get; init; }

    public required string Title { get; init; }

    public required string Summary { get; init; }

    public string? Description { get; init; }

    public required string AccommodationTypeKey { get; init; }

    /// <summary>The catalogue name, resolved. Falls back to the key if the type was retired.</summary>
    public string? AccommodationTypeName { get; init; }

    // ── State ───────────────────────────────────────────────────────────────────────────────

    /// <summary>Draft, Published, Available, Reserved, Occupied, Unavailable or Archived.</summary>
    public required string Status { get; init; }

    public required string Visibility { get; init; }

    /// <summary>Present for the landlord and for staff. Absent for everybody else.</summary>
    public string? ModerationState { get; init; }

    public required bool IsFeatured { get; init; }

    public DateTimeOffset? PublishedAt { get; init; }

    /// <summary>Whether a member may currently ask to view it.</summary>
    public required bool AcceptsViewings { get; init; }

    /// <summary>Whether a member may currently reserve it.</summary>
    public required bool AcceptsReservations { get; init; }

    /// <summary>Whether this landlord asks for a viewing before a reservation.</summary>
    public required bool RequiresViewingBeforeReservation { get; init; }

    // ── Detail ──────────────────────────────────────────────────────────────────────────────

    public required HousingPriceResponse Price { get; init; }

    public required HousingCapacityResponse Capacity { get; init; }

    public required HousingAvailabilityResponse Availability { get; init; }

    public required HousingLocationResponse Location { get; init; }

    /// <summary>Redacted for this viewer. See <see cref="PropertyAddressResponse"/>.</summary>
    public required PropertyAddressResponse Address { get; init; }

    public required StayPolicyResponse StayPolicy { get; init; }

    /// <summary>Present only on shared accommodation.</summary>
    public RoommatePreferencesResponse? RoommatePreferences { get; init; }

    public required string LeaseType { get; init; }

    public required string FurnishedStatus { get; init; }

    public required string Parking { get; init; }

    public required string SmokingPolicy { get; init; }

    public required string PetPolicy { get; init; }

    // ── Collections ─────────────────────────────────────────────────────────────────────────

    public IReadOnlyList<HousingAmenityResponse> Amenities { get; init; } = [];

    public IReadOnlyList<HouseRuleResponse> HouseRules { get; init; } = [];

    /// <summary>Gallery, floor plans and video. Documents are excluded for anybody but the parties.</summary>
    public IReadOnlyList<HousingMediaResponse> Media { get; init; } = [];

    public HousingMediaResponse? CoverImage { get; init; }

    public IReadOnlyList<HousingProviderReferenceResponse> Providers { get; init; } = [];

    /// <summary>
    /// Guides worth reading beside this property. The quietly most valuable field on the page for
    /// somebody about to send a deposit to a stranger in a country they have not arrived in.
    /// </summary>
    public IReadOnlyList<HousingGuideReferenceResponse> Guides { get; init; } = [];

    // ── Parties ─────────────────────────────────────────────────────────────────────────────

    public required LandlordSummaryResponse Landlord { get; init; }

    public required HousingTrustResponse Trust { get; init; }

    public required HousingEngagementResponse Engagement { get; init; }

    // ── Viewer context ──────────────────────────────────────────────────────────────────────

    /// <summary>Whether the caller owns this listing.</summary>
    public required bool IsOwner { get; init; }

    /// <summary>Whether the caller has a viewing that unlocks the address.</summary>
    public required bool HasViewing { get; init; }

    /// <summary>Whether the caller already holds a reservation on it.</summary>
    public required bool HasReservation { get; init; }

    /// <summary>The optimistic-concurrency token an editor sends back.</summary>
    public required int Revision { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset? ModifiedAt { get; init; }
}
