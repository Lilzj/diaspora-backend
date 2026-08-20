using Diaspora.Modules.Community.Domain.Posts;
using Diaspora.Modules.Community.Extensibility.Feeds;
using Diaspora.Modules.Community.Extensibility.Ranking;

namespace Diaspora.Modules.Community.Infrastructure.Feeds;

/// <summary>One organization's published content.</summary>
public sealed class OrganizationFeedProvider : PostFeedProvider
{
    public OrganizationFeedProvider(ICommunityPostRepository posts) : base(posts) { }

    public override string FeedKey => FeedKeys.Organization;

    public override string DefaultRankingKey => RankingKeys.Chronological;

    public override bool CanServe(FeedRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return request.SubjectId is not null;
    }

    protected override PostQueryFilter Narrow(PostQueryFilter filter, FeedRequest request) =>
        filter with
        {
            OrganizationIds = request.SubjectId is { } subject ? [subject] : null
        };
}
