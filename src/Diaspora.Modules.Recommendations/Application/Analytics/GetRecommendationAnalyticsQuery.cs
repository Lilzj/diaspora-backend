using Diaspora.Application.Abstractions;
using Diaspora.Modules.Recommendations.Contracts.V1.Responses;

namespace Diaspora.Modules.Recommendations.Application.Analytics;

/// <summary>
/// How the recommendations are actually performing.
/// <para>
/// Staff-only, and about nobody in particular: every figure here is an aggregate across members, and
/// there is no parameter that could narrow it to one. That is what makes an analytics surface over
/// behavioural data safe to expose at all.
/// </para>
/// </summary>
[RequirePermission(RecommendationPermissions.AnalyticsRead)]
public sealed record GetRecommendationAnalyticsQuery
    : IQuery<IReadOnlyList<RecommendationAnalyticsResponse>>
{
    /// <summary>Null returns every surface.</summary>
    public string? Surface { get; init; }

    /// <summary>Defaults to thirty days ago. Never unbounded — see the handler.</summary>
    public DateOnly? From { get; init; }

    public DateOnly? To { get; init; }

    public int? Limit { get; init; }
}
