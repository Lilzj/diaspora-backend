namespace Diaspora.Modules.Housing.Contracts.V1.Responses;

/// <summary>
/// Where the property is, at neighbourhood resolution.
/// <para>
/// The area identifier is B4's, and it is here so a client can follow through to the neighbourhood
/// profile or to Location itself. Housing owns none of these values.
/// </para>
/// </summary>
public sealed record HousingLocationResponse
{
    public required string CountryCode { get; init; }

    public string? RegionName { get; init; }

    public string? CityName { get; init; }

    public string? NeighborhoodName { get; init; }

    /// <summary>B4's area. The key to the neighbourhood profile.</summary>
    public Guid? NeighborhoodAreaId { get; init; }

    /// <summary>The one-line place, as specific as B4 could make it.</summary>
    public required string DisplayName { get; init; }
}
