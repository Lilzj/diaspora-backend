using Diaspora.Modules.Community.Extensibility.Feeds;

namespace Diaspora.Modules.Community.Extensibility.Ranking;

/// <summary>
/// Engagement per unit of age.
/// <para>
/// The shape is the well-worn one — engagement over a decayed age — and the constants are the
/// part worth explaining rather than the formula.
/// </para>
/// <para>
/// <b>Gravity of 1.5</b> rather than a steeper curve: this platform's content is often practical
/// rather than topical, and a guide to opening a bank account is still useful on day three. A
/// steeper decay optimises for novelty, which is the wrong target for a feed people consult.
/// </para>
/// <para>
/// <b>A comment counts for three reactions.</b> Writing something takes an order of magnitude
/// more intent than tapping, and a trending list that weighted them equally would be a list of
/// whatever was easiest to react to.
/// </para>
/// <para>
/// <b>The two-hour offset</b> stops a post that is four minutes old and has one reaction from
/// dividing by nearly zero and topping the feed forever.
/// </para>
/// </summary>
public sealed class TrendingRanking : IRankingStrategy
{
    private const double Gravity = 1.5d;

    private const double AgeOffsetHours = 2d;

    private const double CommentWeight = 3d;

    public string RankingKey => RankingKeys.Trending;

    public IReadOnlyList<FeedItem> Rank(IReadOnlyList<FeedItem> items, RankingContext context)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(context);

        return items
            .Select(item => item with { SortKey = Score(item, context) })
            .OrderByDescending(item => item.SortKey)
            .ThenByDescending(item => item.PublishedAt ?? DateTimeOffset.MinValue)
            .ToList();
    }

    private static double Score(FeedItem item, RankingContext context)
    {
        var key = item.Reference.Key;

        var reactions = context.ReactionScores.GetValueOrDefault(key);
        var comments = context.CommentCounts.GetValueOrDefault(key);
        var engagement = reactions + (comments * CommentWeight);

        var published = item.PublishedAt ?? context.Now;
        var ageHours = Math.Max(0d, (context.Now - published).TotalHours);

        return engagement / Math.Pow(ageHours + AgeOffsetHours, Gravity);
    }
}
