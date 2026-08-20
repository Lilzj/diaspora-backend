using Diaspora.Modules.Community.Domain.Posts;
using Diaspora.Modules.Community.Domain.ValueObjects;
using Diaspora.Modules.Community.Extensibility.Feeds;
using Diaspora.Modules.Community.Extensibility.Ranking;

namespace Diaspora.Modules.Community.Infrastructure.Feeds;

/// <summary>Everything carrying one hashtag.</summary>
public sealed class HashtagFeedProvider : PostFeedProvider
{
    public HashtagFeedProvider(ICommunityPostRepository posts) : base(posts) { }

    public override string FeedKey => FeedKeys.Hashtag;

    public override string DefaultRankingKey => RankingKeys.Trending;

    public override bool CanServe(FeedRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return !string.IsNullOrWhiteSpace(request.HashtagSlug);
    }

    protected override PostQueryFilter Narrow(PostQueryFilter filter, FeedRequest request) =>
        filter with
        {
            // Normalised here as well as at write time. A client linking to a tag will spell it
            // however the member typed it, and a feed that only matched the canonical form would
            // return nothing for a perfectly good link.
            HashtagSlug = HashtagSlug.Normalise(request.HashtagSlug),
            CountryCode = request.CountryCode
        };
}
