using Diaspora.Modules.Recommendations.Domain.Enums;

namespace Diaspora.Modules.Recommendations.Extensibility.Catalog;

/// <summary>
/// A recommendation-policy row a module contributes at startup.
/// <para>
/// <see cref="IsSystem"/> is honoured by the seeder and refused to a contributor outside this
/// module. A vertical may contribute a rule; it may not contribute an undeletable one, because
/// "undeletable" is a platform guarantee rather than a vertical's preference.
/// </para>
/// </summary>
public sealed record RecommendationPolicySeed
{
    public required string Key { get; init; }

    public required RecommendationPolicyKind Kind { get; init; }

    public required PolicyEffect Effect { get; init; }

    public required string Description { get; init; }

    public int Order { get; init; } = 100;

    /// <summary>Only honoured for policies contributed by this module. See the class summary.</summary>
    public bool IsSystem { get; init; }

    /// <summary>Ignored unless the effect is a demotion. Clamped to (0, 1].</summary>
    public decimal DemotionFactor { get; init; } = 1m;

    /// <summary>Empty means every surface.</summary>
    public IReadOnlyList<RecommendationSurface> Surfaces { get; init; } = [];

    /// <summary>Empty means every target type.</summary>
    public IReadOnlyList<RecommendationTargetType> TargetTypes { get; init; } = [];

    public IReadOnlyDictionary<string, string> Parameters { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);
}
