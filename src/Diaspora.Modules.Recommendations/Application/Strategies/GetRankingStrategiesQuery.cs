using Diaspora.Application.Abstractions;
using Diaspora.Modules.Recommendations.Contracts.V1.Responses;

namespace Diaspora.Modules.Recommendations.Application.Strategies;

/// <summary>
/// The configured ranking strategies.
/// <para>
/// Staff-only. What a member is shown is their business; what everybody is shown is a platform
/// decision, and reading the weights behind it is the first half of being able to change them.
/// </para>
/// </summary>
[RequirePermission(RecommendationPermissions.StrategiesRead)]
public sealed record GetRankingStrategiesQuery : IQuery<IReadOnlyList<RankingStrategyResponse>>
{
    /// <summary>Null returns every surface's configuration.</summary>
    public string? Surface { get; init; }
}
