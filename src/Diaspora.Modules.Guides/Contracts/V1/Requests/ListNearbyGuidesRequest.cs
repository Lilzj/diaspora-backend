namespace Diaspora.Modules.Guides.Contracts.V1.Requests;

/// <summary>
/// Asks for guides relevant to a place, in whatever terms the client has.
/// <para>
/// Every geographic field is optional and every combination is accepted; all of it goes to
/// B4's <c>IGeographicScopeResolver</c> and is normalised once. B8 parses no radius, resolves
/// no city name and computes no distance. The radius sentinels are B4's contract: a positive
/// value is kilometres, <c>-1</c> means "this city", <c>-2</c> means "this country".
/// </para>
/// </summary>
public sealed record ListNearbyGuidesRequest
{
    public string? CityName { get; init; }

    public string? CountryCode { get; init; }

    public Guid? AreaId { get; init; }

    public double? Latitude { get; init; }

    public double? Longitude { get; init; }

    /// <summary>Kilometres, or the sentinels: <c>-1</c> city, <c>-2</c> country.</summary>
    public double? RadiusKm { get; init; }

    /// <summary>A category key, to narrow the answer.</summary>
    public string? Category { get; init; }

    public int? Limit { get; init; }
}
