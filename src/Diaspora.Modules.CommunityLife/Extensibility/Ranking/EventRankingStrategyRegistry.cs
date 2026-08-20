using Microsoft.Extensions.Logging;

namespace Diaspora.Modules.CommunityLife.Extensibility.Ranking;

/// <summary>
/// Combines every registered ranking strategy into one score.
/// <para>
/// A weighted mean rather than a sum, so adding a strategy does not silently inflate every event's
/// score relative to a cached one. A strategy that throws is skipped and logged: somebody else's
/// arithmetic must not take down a browse screen.
/// </para>
/// <para>
/// Scoped rather than singleton, because a strategy may hold a repository — and a singleton
/// registry over scoped implementations is the captive dependency the host's provider validation
/// refuses to start on. B4 recorded that failure first and every module since has repeated the note.
/// </para>
/// </summary>
public sealed class EventRankingStrategyRegistry
{
    private readonly IReadOnlyList<IEventRankingStrategy> _strategies;
    private readonly ILogger<EventRankingStrategyRegistry> _logger;

    public EventRankingStrategyRegistry(
        IEnumerable<IEventRankingStrategy> strategies,
        ILogger<EventRankingStrategyRegistry> logger)
    {
        _strategies = [.. strategies];
        _logger = logger;
    }

    /// <summary>Whether this deployment ranks at all. False means "order by the sort key alone".</summary>
    public bool HasStrategies => _strategies.Count > 0;

    public IReadOnlyList<string> StrategyNames => [.. _strategies.Select(strategy => strategy.Name)];

    /// <summary>
    /// The combined score for one event, nought to one. Returns zero when nothing is registered,
    /// which leaves the caller's own ordering untouched.
    /// </summary>
    public double Score(EventRankingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (_strategies.Count == 0)
        {
            return 0;
        }

        var total = 0d;
        var weight = 0d;

        foreach (var strategy in _strategies)
        {
            try
            {
                var score = Math.Clamp(strategy.Score(context), 0, 1);
                var strategyWeight = Math.Max(0, strategy.Weight);

                total += score * strategyWeight;
                weight += strategyWeight;
            }
#pragma warning disable CA1031 // A contributed strategy must not take down a browse screen.
            catch (Exception exception)
            {
                _logger.LogWarning(
                    exception,
                    "Ranking strategy {Strategy} failed for event {EventId}. Skipping it.",
                    strategy.Name,
                    context.EventId);
            }
#pragma warning restore CA1031
        }

        return weight == 0 ? 0 : total / weight;
    }
}
