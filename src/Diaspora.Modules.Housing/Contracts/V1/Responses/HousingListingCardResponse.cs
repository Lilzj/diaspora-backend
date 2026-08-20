namespace Diaspora.Modules.Housing.Contracts.V1.Responses;

/// <summary>
/// A property as a results page renders it.
/// <para>
/// Deliberately much thinner than the full response: a page of twenty cards that each carried a
/// full listing would be several thousand rows to render twenty titles. The address never appears
/// on a card at all, at any disclosure level - a results page is the last place a street name
/// belongs.
/// </para>
/// </summary>
public sealed record HousingListingCardResponse
{
    public required Guid Id { get; init; }

    public required string Slug { get; init; }

    public required string Title { get; init; }

    public required string Summary { get; init; }

    public required string AccommodationTypeKey { get; init; }

    public string? AccommodationTypeName { get; init; }

    public required string Status { get; init; }

    public required HousingPriceResponse Price { get; init; }

    public required HousingCapacityResponse Capacity { get; init; }

    public required HousingAvailabilityResponse Availability { get; init; }

    public required HousingLocationResponse Location { get; init; }

    public required StayPolicyResponse StayPolicy { get; init; }

    public required string FurnishedStatus { get; init; }

    public HousingMediaResponse? CoverImage { get; init; }

    /// <summary>The chips a card renders. Bounded by the client, not by the server.</summary>
    public IReadOnlyList<HousingAmenityResponse> Amenities { get; init; } = [];

    public required LandlordSummaryResponse Landlord { get; init; }

    public required bool IsLandlordVerified { get; init; }

    public required bool IsFeatured { get; init; }

    public DateTimeOffset? PublishedAt { get; init; }
}
