using Diaspora.Modules.Community.Domain.Posts;
using Diaspora.Modules.Community.Extensibility.Feeds;
using Diaspora.Modules.Community.Extensibility.Ranking;

namespace Diaspora.Modules.Community.Infrastructure.Feeds;

/// <summary>
/// Everything in one administrative area and everything beneath it.
/// <para>
/// The subtree, not the exact node, and that is the whole reason the hierarchy path is copied
/// onto the post. A member reading the feed for a London borough should see the posts filed
/// against the wards inside it; matching the area identifier alone would show them an almost
/// empty feed and no indication why.
/// </para>
/// </summary>
public sealed class NeighborhoodFeedProvider : PostFeedProvider
{
    public NeighborhoodFeedProvider(ICommunityPostRepository posts) : base(posts) { }

    public override string FeedKey => FeedKeys.Neighborhood;

    public override string DefaultRankingKey => RankingKeys.LocationWeighted;

    public override bool CanServe(FeedRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return request.AreaId is not null
               || !string.IsNullOrWhiteSpace(request.HierarchyPathPrefix)
               || !string.IsNullOrWhiteSpace(request.CityName);
    }

    protected override PostQueryFilter Narrow(PostQueryFilter filter, FeedRequest request) =>
        filter with
        {
            AreaId = request.HierarchyPathPrefix is null ? request.AreaId : null,
            HierarchyPathPrefix = request.HierarchyPathPrefix,
            CityName = request.CityName,
            CountryCode = request.CountryCode
        };
}
