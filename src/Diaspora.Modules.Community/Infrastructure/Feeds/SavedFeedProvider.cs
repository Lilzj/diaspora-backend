using Diaspora.Modules.Community.Domain.Bookmarks;
using Diaspora.Modules.Community.Domain.Enums;
using Diaspora.Modules.Community.Domain.Identifiers;
using Diaspora.Modules.Community.Domain.Posts;
using Diaspora.Modules.Community.Domain.ValueObjects;
using Diaspora.Modules.Community.Extensibility.Feeds;
using Diaspora.Modules.Community.Extensibility.Ranking;

namespace Diaspora.Modules.Community.Infrastructure.Feeds;

/// <summary>
/// What the reader has saved.
/// <para>
/// The one built-in feed whose candidate set is <b>not</b> the post table. It reads bookmarks,
/// which span every content type on the platform, and hydrates the posts among them — a saved
/// feed containing a guide, a job and a post is the normal case, and each of those references is
/// returned whether or not B6 owns the item behind it.
/// </para>
/// <para>
/// That is what makes this the feed that proves the design: the same cursor, the same page
/// shape and the same endpoint serve a list whose items belong to four different modules.
/// </para>
/// <para>
/// Ordered by when it was saved, and no ranking is offered. Reordering somebody's own saved list
/// by an engagement score would hide the thing they saved five minutes ago behind something
/// popular, which is the opposite of what saving is for.
/// </para>
/// </summary>
public sealed class SavedFeedProvider : IFeedProvider
{
    private readonly IBookmarkRepository _bookmarks;
    private readonly ICommunityPostRepository _posts;

    public SavedFeedProvider(IBookmarkRepository bookmarks, ICommunityPostRepository posts)
    {
        _bookmarks = bookmarks;
        _posts = posts;
    }

    public string FeedKey => FeedKeys.Saved;

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

        var contentType = request.Parameters.TryGetValue("contentType", out var raw)
            && Enum.TryParse<CommunityContentType>(raw, ignoreCase: true, out var parsed)
                ? parsed
                : (CommunityContentType?)null;

        var folderId = request.Parameters.TryGetValue("folderId", out var rawFolder)
            && Guid.TryParse(rawFolder, out var folderGuid)
                ? BookmarkFolderId.From(folderGuid)
                : null;

        var page = await _bookmarks.ListAsync(
            request.ViewerId, contentType, folderId, request.Cursor, request.Limit, cancellationToken);

        var postIds = page.Items
            .Where(bookmark => bookmark.Content.Type == CommunityContentType.Post)
            .Select(bookmark => CommunityPostId.From(bookmark.Content.Id))
            .ToList();

        var posts = postIds.Count == 0
            ? []
            : (await _posts.GetManyAsync(postIds, cancellationToken))
                .ToDictionary(post => post.Id.Value);

        var items = page.Items
            .Select(bookmark => new FeedItem
            {
                Reference = bookmark.Content,
                Post = posts.GetValueOrDefault(bookmark.Content.Id),
                SortKey = bookmark.SavedAt.ToUnixTimeMilliseconds(),
                PublishedAt = bookmark.SavedAt
            })
            .ToList();

        return FeedPage.Of(items, FeedCursor.Decode(page.NextCursor));
    }
}
