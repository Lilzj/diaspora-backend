using Diaspora.Modules.Community.Domain.Enums;
using Diaspora.Modules.Community.Domain.Posts;
using Diaspora.Modules.Community.Extensibility.Feeds;
using Diaspora.Modules.Community.Extensibility.Ranking;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Community.Infrastructure.Feeds;

/// <summary>
/// The reader's own content, drafts and archives included.
/// <para>
/// The only feed that returns non-published statuses, and it can because the subject is always
/// the reader: the request is rewritten to the viewer's own identifier, so there is no parameter
/// by which one member could ask for another member's drafts.
/// </para>
/// </summary>
public sealed class ProfileFeedProvider : PostFeedProvider
{
    public ProfileFeedProvider(ICommunityPostRepository posts) : base(posts) { }

    public override string FeedKey => FeedKeys.Profile;

    public override string DefaultRankingKey => RankingKeys.Chronological;

    public override bool CanServe(FeedRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return request.ViewerId is not null;
    }

    protected override PostQueryFilter Narrow(PostQueryFilter filter, FeedRequest request) =>
        filter with
        {
            AuthorIds = request.ViewerId is null ? null : [request.ViewerId],
            Statuses =
            [
                PostStatus.Draft,
                PostStatus.Published,
                PostStatus.Expired,
                PostStatus.Archived,
                PostStatus.Hidden
            ],
            ExcludeExpired = false
        };
}
