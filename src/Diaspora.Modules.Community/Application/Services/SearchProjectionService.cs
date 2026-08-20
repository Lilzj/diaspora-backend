using Diaspora.Modules.Community.Domain.Enums;
using Diaspora.Modules.Community.Domain.Policies;
using Diaspora.Modules.Community.Domain.Posts;
using Diaspora.Modules.Community.Domain.ValueObjects;
using Diaspora.Modules.Community.Integration;
using Diaspora.SharedKernel.Pagination;

namespace Diaspora.Modules.Community.Application.Services;

/// <summary>
/// Produces the incremental corpus B15 will index.
/// <para>
/// Implemented now, with no search engine behind it, because the expensive part of retrofitting
/// search is never the engine — it is discovering that the content module cannot produce a
/// stable, paginated, incremental projection of its own corpus. This can, from the first day,
/// and the brief asks for exactly that under "search readiness".
/// </para>
/// <para>
/// Deleted and removed posts are emitted with <see cref="SearchProjection.IsDeleted"/> set,
/// rather than filtered out. A projection that only emits live content can never tell an index to
/// <em>remove</em> a document, which is the classic reason deleted posts stay searchable for
/// months.
/// </para>
/// </summary>
public sealed class SearchProjectionService : ICommunitySearchProjectionSource
{
    private readonly ICommunityPostRepository _posts;

    public SearchProjectionService(ICommunityPostRepository posts) => _posts = posts;

    public async Task<CursorPagedResult<SearchProjection>> GetChangedAsync(
        DateTimeOffset since,
        string? cursor,
        int limit,
        CancellationToken cancellationToken = default)
    {
        // Every status, including the ones that mean "stop showing this". An indexer needs the
        // removals as much as the additions.
        var filter = new PostQueryFilter
        {
            Statuses =
            [
                PostStatus.Published,
                PostStatus.Expired,
                PostStatus.Archived,
                PostStatus.Hidden,
                PostStatus.Removed
            ],
            ExcludeExpired = false,
            PublishedAfter = since,
            Cursor = FeedCursor.Decode(cursor),
            Limit = Math.Clamp(limit, 1, CommunityLimits.MaxFeedPageSize)
        };

        var page = await _posts.QueryAsync(filter, cancellationToken);

        var projections = page.Items.Select(Project).ToList();

        return CursorPagedResult<SearchProjection>.Create(
            projections, page.NextCursor, page.HasMore);
    }

    private static SearchProjection Project(CommunityPost post) => new()
    {
        Content = post.Reference,
        Title = post.Title ?? string.Empty,
        Body = post.Body.Value,
        AuthorId = post.AuthorId.Value,
        Category = CommunityWireNames.ToWire(post.Category),
        Hashtags = post.Hashtags.Select(hashtag => hashtag.Slug).ToList(),
        CountryCode = post.Location.CountryCode,
        AreaId = post.Location.AreaId,
        CityName = post.Location.CityName,
        Latitude = post.Location.Latitude,
        Longitude = post.Location.Longitude,

        // Carried so an index can filter without a round trip. An index that stores no
        // visibility can only be queried by a process that re-checks every hit, which makes
        // search slower than the feed it was meant to accelerate.
        Visibility = CommunityWireNames.ToWire(post.Audience.Level),
        PublishedAt = post.PublishedAt,
        UpdatedAt = post.EditedAt ?? post.PublishedAt ?? post.CreatedAt,
        IsDeleted = post.IsDeleted || post.Status is PostStatus.Removed or PostStatus.Deleted,
        EngagementScore =
            post.Counters.ReactionCount + post.Counters.CommentCount + post.Counters.ShareCount
    };
}
