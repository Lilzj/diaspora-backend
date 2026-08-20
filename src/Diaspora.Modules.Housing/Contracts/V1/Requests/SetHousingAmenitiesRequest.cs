namespace Diaspora.Modules.Housing.Contracts.V1.Requests;

/// <summary>Replaces the amenity list wholesale.</summary>
public sealed record SetHousingAmenitiesRequest
{
    public required IReadOnlyList<HousingAmenityRequest> Amenities { get; init; }

    public int? BaseRevision { get; init; }
}
