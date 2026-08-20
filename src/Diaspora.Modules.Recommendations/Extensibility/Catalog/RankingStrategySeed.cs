using Diaspora.Modules.Recommendations.Domain.Enums;

namespace Diaspora.Modules.Recommendations.Extensibility.Catalog;

/// <summary>
/// A ranking-strategy row a module contributes at startup.
/// <para>
/// A flat description rather than the aggregate, for the reason every contributor in this platform
/// takes one: contributors are constructed by the kernel before the container exists, so they cannot
/// depend on a clock, an identifier generator or a repository. The seeder turns these into
/// aggregates once the host is running.
/// </para>
/// </summary>
public sealed record RankingStrategySeed
{
    public required string Key { get; init; }

    public required RankingStrategyKind Kind { get; init; }

    public required RecommendationSurface Surface { get; init; }

    public required string Description { get; init; }

    /// <summary>0–1. What this strategy is worth on this surface.</summary>
    public required decimal Weight { get; init; }

    public RecommendationTargetType? TargetType { get; init; }

    public int Order { get; init; } = 100;

    public bool RunsOnColdStart { get; init; }

    public decimal MinimumScore { get; init; }

    public IReadOnlyDictionary<string, string> Parameters { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);
}
