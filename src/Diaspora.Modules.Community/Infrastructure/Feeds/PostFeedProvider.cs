using Diaspora.Modules.Community.Domain.Enums;
using Diaspora.Modules.Community.Domain.Posts;
using Diaspora.Modules.Community.Domain.ValueObjects;
using Diaspora.Modules.Community.Extensibility.Feeds;

namespace Diaspora.Modules.Community.Infrastructure.Feeds;

/// <summary>
/// The shared body of every feed B6 ships over its own posts.
/// <para>
/// A base class rather than eleven copies of the same query, because the eleven differ only in
/// how they narrow the filter — and a copy-per-feed is how a visibility clause ends up applied
/// in ten feeds and forgotten in the eleventh.
/// </para>
/// <para>
/// The visibility and block filters are applied <b>here</b>, in the base, from values the
/// request carries. A subclass overrides <see cref="Narrow"/> and cannot remove them.
/// </para>
/// </summary>
public abstract class PostFeedProvider : IFeedProvider
{
    protected PostFeedProvider(ICommunityPostRepository posts) => Posts = posts;

    protected ICommunityPostRepository Posts { get; }

    public abstract string FeedKey { get; }

    public abstract string DefaultRankingKey { get; }

    public virtual bool CanServe(FeedRequest request) => true;

    public async Task<FeedPage> GetAsync(
        FeedRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var filter = Narrow(BaseFilter(request), request);
        var page = await Posts.QueryAsync(filter, cancellationToken);

        var items = page.Items
            .Select(post => FeedItem.ForPost(
                post, (post.PublishedAt ?? post.CreatedAt).ToUnixTimeMilliseconds()))
            .ToList();

        return FeedPage.Of(items, FeedCursor.Decode(page.NextCursor));
    }

    /// <summary>The filter every feed starts from. Never widened by a subclass.</summary>
    protected static PostQueryFilter BaseFilter(FeedRequest request) => new()
    {
        Statuses = request.IncludeExpired
            ? [PostStatus.Published, PostStatus.Expired]
            : [PostStatus.Published],
        AllowedVisibility = request.AllowedVisibility,
        NeighborhoodViewer = request.NeighborhoodViewer,
        NeighborhoodAuthorId = request.ViewerId,
        ExcludedAuthorIds = request.ExcludedAuthorIds,
        ExcludeExpired = !request.IncludeExpired,
        Category = request.Category,
        MinimumUrgency = request.MinimumUrgency,
        Cursor = request.Cursor,
        Limit = request.Limit
    };

    /// <summary>Where a feed says what makes it different.</summary>
    protected abstract PostQueryFilter Narrow(PostQueryFilter filter, FeedRequest request);
}
