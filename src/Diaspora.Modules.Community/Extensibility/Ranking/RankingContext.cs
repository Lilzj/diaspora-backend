using Diaspora.Modules.Community.Extensibility.Feeds;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Community.Extensibility.Ranking;

/// <summary>
/// What a ranking strategy is allowed to know.
/// <para>
/// The signals are resolved once, before ranking, and handed in — a strategy never issues a
/// query. That is what makes ranking cheap enough to be replaceable: a strategy that could load
/// data would make swapping one a performance decision as well as a product one, and nobody
/// swaps a ranking they have to benchmark first.
/// </para>
/// <para>
/// Trust levels and distances arrive as dictionaries because they are batch-resolved for the
/// whole candidate set. A strategy asking Trust per item would issue one call per card.
/// </para>
/// </summary>
public sealed record RankingContext
{
    public required FeedRequest Request { get; init; }

    public required DateTimeOffset Now { get; init; }

    /// <summary>Author identifier to that author's platform trust level, as an ordinal.</summary>
    public IReadOnlyDictionary<Guid, int> AuthorTrustLevels { get; init; } =
        new Dictionary<Guid, int>();

    /// <summary>Content key to distance in kilometres. Every value came from Location.</summary>
    public IReadOnlyDictionary<string, double> Distances { get; init; } =
        new Dictionary<string, double>(StringComparer.Ordinal);

    /// <summary>Content key to its weighted reaction score.</summary>
    public IReadOnlyDictionary<string, double> ReactionScores { get; init; } =
        new Dictionary<string, double>(StringComparer.Ordinal);

    /// <summary>Content key to comment count.</summary>
    public IReadOnlyDictionary<string, int> CommentCounts { get; init; } =
        new Dictionary<string, int>(StringComparer.Ordinal);

    /// <summary>Whom the viewer follows, for strategies that boost them.</summary>
    public IReadOnlySet<Guid> FollowedAuthorIds { get; init; } = new HashSet<Guid>();

    public UserId? ViewerId => Request.ViewerId;
}
