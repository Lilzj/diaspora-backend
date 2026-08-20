using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Recommendations.Application.Mapping;
using Diaspora.Modules.Recommendations.Contracts.V1.Responses;
using Diaspora.Modules.Recommendations.Domain.Identifiers;
using Diaspora.Modules.Recommendations.Domain.Strategies;
using Diaspora.Modules.Recommendations.Extensibility.Caching;
using Diaspora.Modules.Recommendations.Extensibility.Ranking;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Recommendations.Application.Strategies;

/// <summary>
/// Applies a retune, and clears the cache so it takes effect now.
/// <para>
/// <b>The cache invalidation is not an afterthought.</b> Strategy rows are cached for minutes;
/// without clearing them an operator would change a weight, watch a surface that did not move,
/// change it again, and end up with a double adjustment landing at once. Every write path in this
/// module that touches the catalogue clears the prefix.
/// </para>
/// </summary>
public sealed class UpdateRankingStrategyCommandHandler
    : ICommandHandler<UpdateRankingStrategyCommand, RankingStrategyResponse>
{
    /// <summary>Matches the engine's cache key prefix. Changing one means changing both.</summary>
    private const string StrategyCachePrefix = "recommendations:strategies:";

    private readonly IRankingStrategyRepository _strategies;
    private readonly IRankingStrategyRegistry _registry;
    private readonly IRecommendationCache _cache;
    private readonly IGuidGenerator _guids;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateRankingStrategyCommandHandler(
        IRankingStrategyRepository strategies,
        IRankingStrategyRegistry registry,
        IRecommendationCache cache,
        IGuidGenerator guids,
        IUnitOfWork unitOfWork)
    {
        _strategies = strategies;
        _registry = registry;
        _cache = cache;
        _guids = guids;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<RankingStrategyResponse>> Handle(
        UpdateRankingStrategyCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var strategy = await _strategies.FindAsync(
            RankingStrategyId.From(command.StrategyId), cancellationToken);

        if (strategy is null)
        {
            return Result.Failure<RankingStrategyResponse>(Error.NotFound(
                RecommendationErrorCodes.StrategyNotFound,
                "That ranking strategy does not exist."));
        }

        Apply(command, strategy);

        _strategies.Update(strategy);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // After the save, not before: clearing a cache in front of a change that then failed to
        // commit would make every instance re-read the old row and cache it again.
        await _cache.RemoveByPrefixAsync(StrategyCachePrefix, cancellationToken);

        return Result.Success(
            RecommendationMapper.ToResponse(strategy, _registry.IsImplemented(strategy.Key)));
    }

    private void Apply(UpdateRankingStrategyCommand command, RankingStrategy strategy)
    {
        if (command.Weight is { } weight) strategy.SetWeight(weight);
        if (command.Order is { } order) strategy.SetOrder(order);
        if (command.MinimumScore is { } minimum) strategy.SetMinimumScore(minimum);
        if (command.RunsOnColdStart is { } coldStart) strategy.SetRunsOnColdStart(coldStart);
        if (command.Description is not null) strategy.Describe(command.Description);

        if (command.IsEnabled is { } enabled)
        {
            if (enabled)
            {
                strategy.Enable();
            }
            else
            {
                strategy.Disable();
            }
        }

        foreach (var (key, value) in command.Parameters ?? new Dictionary<string, string>())
        {
            strategy.SetParameter(key, value, _guids.NewSequentialGuid());
        }
    }
}
