using Diaspora.Modules.Community.Domain.Posts;
using Diaspora.Modules.Community.Extensibility.Feeds;
using Diaspora.Modules.Community.Extensibility.Ranking;

namespace Diaspora.Modules.Community.Infrastructure.Feeds;

/// <summary>
/// The default feed.
/// <para>
/// Scoped to the reader's country when one is known, and ranked by location weight — which
/// reproduces what the client currently does by hand on its home screen: urgent nearby items
/// first, then everything else by proximity.
/// </para>
/// <para>
/// Deliberately <b>not</b> scoped to what the reader follows. A diaspora member's home feed
/// showing only people they already follow is a home feed that is empty on the day they need it,
/// which is the day they arrive somewhere new. Following has its own feed.
/// </para>
/// </summary>
public sealed class HomeFeedProvider : PostFeedProvider
{
    public HomeFeedProvider(ICommunityPostRepository posts) : base(posts) { }

    public override string FeedKey => FeedKeys.Home;

    public override string DefaultRankingKey => RankingKeys.LocationWeighted;

    protected override PostQueryFilter Narrow(PostQueryFilter filter, FeedRequest request) =>
        filter with
        {
            CountryCode = request.CountryCode,
            PinnedFirst = true
        };
}
