namespace Diaspora.Modules.Housing.Contracts.V1.Requests;

/// <summary>
/// Revises a property.
/// <para>
/// Every field is optional and null means leave it alone, so a client editing the rent need not
/// round-trip the other thirty and cannot blank a field by omitting it.
/// </para>
/// </summary>
public sealed record UpdateHousingListingRequest
{
    public string? Title { get; init; }

    public string? Summary { get; init; }

    public string? Description { get; init; }

    public string? AccommodationTypeKey { get; init; }

    public decimal? RentAmount { get; init; }

    public string? CurrencyCode { get; init; }

    public string? RentPeriod { get; init; }

    public decimal? DepositAmount { get; init; }

    public bool? IsNegotiable { get; init; }

    public bool? UtilitiesIncluded { get; init; }

    public bool? InternetIncluded { get; init; }

    public int? Bedrooms { get; init; }

    public decimal? Bathrooms { get; init; }

    public int? MaximumOccupants { get; init; }

    public int? FloorAreaSquareMetres { get; init; }

    public string? LeaseType { get; init; }

    public string? FurnishedStatus { get; init; }

    public string? Parking { get; init; }

    public string? SmokingPolicy { get; init; }

    public string? PetPolicy { get; init; }

    public bool? RequiresViewingBeforeReservation { get; init; }

    public string? RegionName { get; init; }

    public string? CityName { get; init; }

    public Guid? NeighborhoodAreaId { get; init; }

    public string? AddressLine1 { get; init; }

    public string? AddressLine2 { get; init; }

    public string? PostalCode { get; init; }

    public double? Latitude { get; init; }

    public double? Longitude { get; init; }

    public string? AddressVisibility { get; init; }

    /// <summary>The revision the client loaded. A stale one is refused with a conflict.</summary>
    public int? BaseRevision { get; init; }
}
