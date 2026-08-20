using Diaspora.Modules.Community.Domain.Enums;
using Diaspora.Modules.Community.Domain.Posts;
using Diaspora.Modules.Community.Extensibility.Feeds;
using Diaspora.Modules.Community.Extensibility.Ranking;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Community.Infrastructure.Feeds;

/// <summary>
/// One member's published content. The client's contributor screen.
/// <para>
/// Includes expired posts, unlike every other public feed. A contributor page is a body of work
/// rather than a live feed, and a member whose profile emptied itself as their traffic alerts
/// expired would look like a member who had never posted anything.
/// </para>
/// </summary>
public sealed class ContributorFeedProvider : PostFeedProvider
{
    public ContributorFeedProvider(ICommunityPostRepository posts) : base(posts) { }

    public override string FeedKey => FeedKeys.Contributor;

    public override string DefaultRankingKey => RankingKeys.Chronological;

    public override bool CanServe(FeedRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return request.SubjectId is not null;
    }

    protected override PostQueryFilter Narrow(PostQueryFilter filter, FeedRequest request) =>
        filter with
        {
            AuthorIds = request.SubjectId is { } subject ? [UserId.From(subject)] : null,
            Statuses = [PostStatus.Published, PostStatus.Expired],
            ExcludeExpired = false
        };
}
