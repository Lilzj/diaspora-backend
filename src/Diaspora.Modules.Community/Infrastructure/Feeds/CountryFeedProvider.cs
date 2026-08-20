using Diaspora.Modules.Community.Domain.Posts;
using Diaspora.Modules.Community.Extensibility.Feeds;
using Diaspora.Modules.Community.Extensibility.Ranking;

namespace Diaspora.Modules.Community.Infrastructure.Feeds;

/// <summary>Everything happening in one country.</summary>
public sealed class CountryFeedProvider : PostFeedProvider
{
    public CountryFeedProvider(ICommunityPostRepository posts) : base(posts) { }

    public override string FeedKey => FeedKeys.Country;

    public override string DefaultRankingKey => RankingKeys.TrustWeighted;

    public override bool CanServe(FeedRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return !string.IsNullOrWhiteSpace(request.CountryCode);
    }

    protected override PostQueryFilter Narrow(PostQueryFilter filter, FeedRequest request) =>
        filter with { CountryCode = request.CountryCode };
}
