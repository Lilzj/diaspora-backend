namespace Diaspora.Modules.Recommendations.Contracts.V1.Responses;

/// <summary>
/// Where a recommended thing is.
/// <para>
/// <b>Nested rather than flattened onto the card, because the mobile client's existing card models
/// already nest it.</b> Every card type in the app — the listing card, the guide card, the update
/// card — carries a <c>location</c> object with a city and an area name, and a backend that returned
/// four sibling properties would force a mapping layer on the client for no benefit.
/// </para>
/// <para>
/// <b>No coordinates and no distance.</b> This module holds neither: it ranks on named-place matching
/// rather than geometry, and returning a distance it had not computed would be a field the client
/// would reasonably trust.
/// </para>
/// </summary>
public sealed record RecommendationLocationResponse
{
    /// <summary>ISO 3166-1 alpha-2, upper case. Null on content that belongs to nowhere.</summary>
    public string? CountryCode { get; init; }

    public string? RegionName { get; init; }

    public string? CityName { get; init; }

    /// <summary>The client's card models call this the area name.</summary>
    public string? NeighborhoodName { get; init; }
}
