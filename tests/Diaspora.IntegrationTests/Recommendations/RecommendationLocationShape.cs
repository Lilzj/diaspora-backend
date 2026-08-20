namespace Diaspora.IntegrationTests.Recommendations;

/// <summary>Where a recommended thing is, as the client's card models nest it.</summary>
public sealed record RecommendationLocationShape
{
    public string? CountryCode { get; init; }

    public string? RegionName { get; init; }

    public string? CityName { get; init; }

    public string? NeighborhoodName { get; init; }
}
