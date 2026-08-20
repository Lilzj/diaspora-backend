using Diaspora.Modules.Community.Domain.Posts;
using Diaspora.Modules.Community.Extensibility.Feeds;
using Diaspora.Modules.Community.Extensibility.Ranking;
using Diaspora.Platform.Services.Abstractions;

namespace Diaspora.Modules.Community.Infrastructure.Feeds;

/// <summary>
/// What is gaining attention.
/// <para>
/// Bounded to a window before ranking, and the window is the whole design. Trending computed
/// over all content is a leaderboard of the most popular posts ever written; trending computed
/// over three days is a list of what is happening. The window also bounds the candidate set,
/// which is what stops the ranker sorting the entire table in memory.
/// </para>
/// </summary>
public sealed class TrendingFeedProvider : PostFeedProvider
{
    private static readonly TimeSpan Window = TimeSpan.FromDays(3);

    private readonly IClock _clock;

    public TrendingFeedProvider(ICommunityPostRepository posts, IClock clock) : base(posts) =>
        _clock = clock;

    public override string FeedKey => FeedKeys.Trending;

    public override string DefaultRankingKey => RankingKeys.Trending;

    protected override PostQueryFilter Narrow(PostQueryFilter filter, FeedRequest request) =>
        filter with
        {
            CountryCode = request.CountryCode,
            PublishedAfter = _clock.UtcNow - Window,

            // Over-fetch so the ranker has something to rank. A trending feed that ranked
            // exactly one page of chronological results would return the newest twenty posts in
            // a different order, which is not trending.
            Limit = Math.Min(
                Domain.Policies.CommunityLimits.MaxFeedPageSize, Math.Max(request.Limit * 3, 30))
        };
}
