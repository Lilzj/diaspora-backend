using Diaspora.Modules.Recommendations.Extensibility.Engine;

namespace Diaspora.Modules.Recommendations.Extensibility.Feed;

/// <summary>One section, filled.</summary>
public sealed record ComposedFeedSection
{
    public required FeedSectionDefinition Definition { get; init; }

    public required IReadOnlyList<ScoredRecommendation> Items { get; init; }

    /// <summary>Whether this section's items came from the cold-start path.</summary>
    public bool IsColdStart { get; init; }
}
