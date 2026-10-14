using Diaspora.Modules.Community.Domain.Enums;
using Diaspora.Modules.Community.Domain.Follows;
using Diaspora.Modules.Community.Domain.Posts;
using Diaspora.Modules.Community.Domain.ValueObjects;
using Diaspora.Modules.Community.Extensibility.Feeds;
using Diaspora.Modules.Community.Extensibility.Ranking;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Community.Infrastructure.Feeds;

/// <summary>
/// Content from whom the reader follows.
/// <para>
/// The one built-in feed that does not derive from the shared base, because its candidate set
/// comes from the follow graph rather than from a geographic or categorical narrowing, and it
/// has to answer for members <em>and</em> organizations in one page.
/// </para>
/// <para>
/// A reader who follows nobody gets an empty page rather than a fallback to popular content.
/// Quietly filling an empty following feed with strangers is how a member stops being able to
/// tell the two feeds apart, and then stops trusting either.
/// </para>
/// </summary>
public sealed class FollowingFeedProvider : IFeedProvider
{
    private readonly ICommunityPostRepository _posts;
    private readonly IFollowRepository _follows;

    public FollowingFeedProvider(ICommunityPostRepository posts, IFollowRepository follows)
    {
        _posts = posts;
        _follows = follows;
    }

    public string FeedKey => FeedKeys.Following;

    public string DefaultRankingKey => RankingKeys.Chronological;

    public bool CanServe(FeedRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return request.ViewerId is not null;
    }

    public async Task<FeedPage> GetAsync(
        FeedRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.ViewerId is null)
        {
            return FeedPage.Empty;
        }

        var members = await _follows.ListTargetsAsync(
            request.ViewerId, FollowTargetType.Member, includeMuted: false, cancellationToken);

        var organizations = await _follows.ListTargetsAsync(
            request.ViewerId, FollowTargetType.Organization, includeMuted: false, cancellationToken);

        if (members.Count == 0 && organizations.Count == 0)
        {
            return FeedPage.Empty;
        }

        var filter = new PostQueryFilter
        {
            Statuses = [PostStatus.Published],
            AllowedVisibility = request.AllowedVisibility,
            NeighborhoodViewer = request.NeighborhoodViewer,
            NeighborhoodAuthorId = request.ViewerId,
            ExcludedAuthorIds = request.ExcludedAuthorIds,
            AuthorIds = members.Select(UserId.From).ToList(),
            OrganizationIds = organizations,
            Category = request.Category,
            Cursor = request.Cursor,
            Limit = request.Limit
        };

        var page = await _posts.QueryAsync(filter, cancellationToken);

        var items = page.Items
            .Select(post => FeedItem.ForPost(
                post, (post.PublishedAt ?? post.CreatedAt).ToUnixTimeMilliseconds()))
            .ToList();

        return FeedPage.Of(items, FeedCursor.Decode(page.NextCursor));
    }
}
