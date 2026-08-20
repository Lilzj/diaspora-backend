using Diaspora.Application.Abstractions;
using Diaspora.Modules.Recommendations.Application.Mapping;
using Diaspora.Modules.Recommendations.Application.Services;
using Diaspora.Modules.Recommendations.Contracts.V1.Responses;
using Diaspora.Modules.Recommendations.Domain.Strategies;
using Diaspora.Modules.Recommendations.Extensibility.Ranking;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Recommendations.Application.Strategies;

/// <summary>
/// Reads the strategy catalogue, with the registry's answer alongside it.
/// <para>
/// <b>The <c>IsImplemented</c> flag is why this handler takes the registry.</b> A configured strategy
/// nothing implements contributes silent zeroes to every score on its surface, and there is no other
/// screen in the platform where that mismatch would be visible.
/// </para>
/// </summary>
public sealed class GetRankingStrategiesQueryHandler
    : IQueryHandler<GetRankingStrategiesQuery, IReadOnlyList<RankingStrategyResponse>>
{
    private readonly IRankingStrategyRepository _strategies;
    private readonly IRankingStrategyRegistry _registry;

    public GetRankingStrategiesQueryHandler(
        IRankingStrategyRepository strategies, IRankingStrategyRegistry registry)
    {
        _strategies = strategies;
        _registry = registry;
    }

    public async Task<Result<IReadOnlyList<RankingStrategyResponse>>> Handle(
        GetRankingStrategiesQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        IReadOnlyList<RankingStrategy> configured;

        if (string.IsNullOrWhiteSpace(query.Surface))
        {
            configured = await _strategies.ListAllAsync(cancellationToken);
        }
        else
        {
            var surface = RecommendationAuthorizationService.ParseSurface(query.Surface);

            if (surface.IsFailure)
            {
                return Result.Failure<IReadOnlyList<RankingStrategyResponse>>(surface.Error);
            }

            configured = await _strategies.ListForSurfaceAsync(surface.Value, cancellationToken);
        }

        return Result.Success<IReadOnlyList<RankingStrategyResponse>>(
            configured
                .Select(strategy => RecommendationMapper.ToResponse(
                    strategy, _registry.IsImplemented(strategy.Key)))
                .ToList());
    }
}
