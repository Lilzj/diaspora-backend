using Diaspora.Application.Abstractions;
using Diaspora.Modules.Recommendations.Contracts.V1.Responses;

namespace Diaspora.Modules.Recommendations.Application.Strategies;

/// <summary>
/// Retunes one surface's ranking.
/// <para>
/// <b>The most consequential write in the module, and the audit entry is the point of it.</b> One
/// number here moves what an entire country sees, with no error, no failed request and no change in
/// any response shape — so the only way anybody will ever reconstruct why a surface's behaviour
/// changed on a particular Tuesday is the audit trail.
/// </para>
/// </summary>
[RequirePermission(RecommendationPermissions.StrategiesManage)]
[Transactional(Reason = "A retune is one decision across a weight, an order and its parameters.")]
[Auditable(Action = "recommendations.strategy.update")]
public sealed record UpdateRankingStrategyCommand : ICommand<RankingStrategyResponse>
{
    public required Guid StrategyId { get; init; }

    public decimal? Weight { get; init; }

    public int? Order { get; init; }

    public bool? IsEnabled { get; init; }

    public bool? RunsOnColdStart { get; init; }

    public decimal? MinimumScore { get; init; }

    public string? Description { get; init; }

    public IReadOnlyDictionary<string, string>? Parameters { get; init; }
}
