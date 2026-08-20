using Diaspora.Application.Abstractions;
using Diaspora.Modules.Recommendations.Contracts.V1.Responses;

namespace Diaspora.Modules.Recommendations.Application.Feed;

/// <summary>
/// Recommendations for one surface.
/// <para>
/// <b>One query serves every per-vertical surface — jobs, housing, marketplace, providers, guides,
/// events, organizations, neighbourhoods, communities, learning paths.</b> Ten near-identical
/// handlers would be ten places for the policy pass to be forgotten, and the only difference between
/// them is a surface name that the catalogue already turns into ten different rankings.
/// </para>
/// </summary>
[Authorize(Permission = RecommendationPermissions.FeedRead)]
public sealed record GetRecommendationsQuery : IQuery<RecommendationPageResponse>
{
    /// <summary>Which surface. Decides the weights, the policies and the diversity rules.</summary>
    public required string Surface { get; init; }

    /// <summary>Null asks for whatever the surface holds.</summary>
    public string? TargetType { get; init; }

    public int? Limit { get; init; }

    /// <summary>Narrows to categories. Empty accepts everything the surface holds.</summary>
    public IReadOnlyList<string> Categories { get; init; } = [];

    /// <summary>Overrides the member's country — the country-page case.</summary>
    public string? CountryCode { get; init; }

    /// <summary>What a pull-to-refresh sets. Ignores a fresh stored page and ranks again.</summary>
    public bool Refresh { get; init; }
}
