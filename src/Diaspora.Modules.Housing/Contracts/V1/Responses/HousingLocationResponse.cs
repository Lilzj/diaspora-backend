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

    /// <summary>
    /// Where a stranger is told the property is: exact only when the landlord made the address
    /// public, and otherwise rounded to two decimal places, about a kilometre. Null when the
    /// property has no pin.
    /// <para>
    /// It is <c>PropertyAddress.PublicCoordinates</c>, the point every distance and "nearest first"
    /// ordering is already measured from, and never more precise than the address an anonymous
    /// caller is shown on the detail. The same for every caller, the owner included: the viewer's
    /// own entitlement is <c>Address</c>, on the detail. Here because a map places search results,
    /// and until this was added no home could be placed (RMD-007).
    /// </para>
    /// </summary>
    public double? Latitude { get; init; }

    /// <inheritdoc cref="Latitude"/>
    public double? Longitude { get; init; }

    /// <summary>The one-line place, as specific as B4 could make it.</summary>
    public required string DisplayName { get; init; }
}
