using Diaspora.Application.Abstractions;
using Diaspora.Modules.Recommendations.Contracts.V1.Responses;

namespace Diaspora.Modules.Recommendations.Application.Search;

/// <summary>
/// Personalization boosts for the caller, for a search engine to consume.
/// <para>
/// Returns nothing when the member has turned personalization off, which is how a privacy preference
/// reaches a surface this module does not own.
/// </para>
/// </summary>
[RequirePermission(RecommendationPermissions.FeedRead)]
public sealed record GetSearchSignalsQuery : IQuery<IReadOnlyList<SearchBoostResponse>>
{
    /// <summary>Asks for the non-personalised trending boosts instead of the caller's own.</summary>
    public bool Trending { get; init; }

    /// <summary>Trending only. Null is global.</summary>
    public string? CountryCode { get; init; }

    /// <summary>Trending only. Null spans every target type.</summary>
    public string? TargetType { get; init; }
}
