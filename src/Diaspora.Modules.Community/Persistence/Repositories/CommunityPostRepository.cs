using System.Diagnostics.CodeAnalysis;
using Diaspora.Modules.Community.Domain.Enums;
using Diaspora.Modules.Community.Domain.Identifiers;
using Diaspora.Modules.Community.Domain.Policies;
using Diaspora.Modules.Community.Domain.Posts;
using Diaspora.Modules.Community.Domain.ValueObjects;
using Diaspora.Persistence;
using Diaspora.Persistence.Paging;
using Diaspora.Persistence.Querying;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Pagination;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Community.Persistence.Repositories;

/// <summary>
/// EF Core repository for posts.
/// <para>
/// <see cref="QueryAsync"/> is the one query every feed runs. It translates a
/// <see cref="PostQueryFilter"/> — a plain record — into a query, which is what lets a
/// contributed feed provider narrow a feed without ever composing an expression tree that might
/// not translate.
/// </para>
/// <para>
/// The cursor is a keyset, not an offset: the page after a given timestamp and identifier rather
/// than the page at a given distance. A feed changes while somebody scrolls, and an offset page
/// two re-shows whatever page one already showed.
/// </para>
/// </summary>
public sealed class CommunityPostRepository : ICommunityPostRepository
{
    private readonly DiasporaDbContext _dbContext;

    public CommunityPostRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<CommunityPost> Posts => _dbContext.Set<CommunityPost>();

    public async Task<CommunityPost?> GetAsync(
        CommunityPostId id, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);

        return await Posts
            .Include(post => post.Attachments)
            .Include(post => post.Hashtags)
            .Include(post => post.Mentions)
            .Include(post => post.Revisions)
            .Include(post => post.Poll!.Options)
            .FirstOrDefaultAsync(post => post.Id == id, cancellationToken);
    }

    /// <summary>The read graph. Revisions are excluded — nothing on a detail screen renders them.</summary>
    public async Task<CommunityPost?> GetForReadAsync(
        CommunityPostId id, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);

        return await Posts
            .Include(post => post.Attachments)
            .Include(post => post.Hashtags)
            .Include(post => post.Mentions)
            .Include(post => post.Poll!.Options)
            .FirstOrDefaultAsync(post => post.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<CommunityPost>> GetManyAsync(
        IReadOnlyCollection<CommunityPostId> ids, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ids);

        if (ids.Count == 0)
        {
            return [];
        }

        return await Posts
            .Include(post => post.Attachments)
            .Include(post => post.Hashtags)
            .Where(post => ids.Contains(post.Id))
            .ToListAsync(cancellationToken);
    }

    public async Task<CursorPagedResult<CommunityPost>> QueryAsync(
        PostQueryFilter filter, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var query = Posts
            .Include(post => post.Attachments)
            .Include(post => post.Hashtags)
            .Include(post => post.Poll!.Options)
            .AsQueryable();

        query = Apply(query, filter);

        // One more than asked for, so "is there another page" needs no count.
        var limit = Math.Clamp(filter.Limit, 1, CommunityLimits.MaxFeedPageSize);

        var ordered = filter.PinnedFirst
            ? query.OrderByDescending(post => post.IsPinned)
                .ThenByDescending(post => post.PublishedAt)
                .ThenByDescending(post => post.Id)
            : query.OrderByDescending(post => post.PublishedAt)
                .ThenByDescending(post => post.Id);

        var items = await ordered.Take(limit + 1).ToListAsync(cancellationToken);

        var hasMore = items.Count > limit;

        if (hasMore)
        {
            items.RemoveAt(items.Count - 1);
        }

        var next = hasMore && items.Count > 0
            ? FeedCursor.FromTimestamp(
                items[^1].PublishedAt ?? items[^1].CreatedAt,
                items[^1].Id.Value,
                FeedBoundary.Key(
                    items,
                    post => post.PublishedAt ?? post.CreatedAt,
                    post => post.Id.Value,
                    filter.Cursor),
                items[^1].IsPinned).Encode()
            : null;

        return CursorPagedResult<CommunityPost>.Create(items, next, hasMore);
    }

    public async Task<IReadOnlyList<CommunityPost>> ListDraftsAsync(
        UserId authorId, int limit, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(authorId);

        return await Posts
            .Where(post => post.AuthorId == authorId && post.Status == PostStatus.Draft)
            .OrderByDescending(post => post.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CommunityPost>> ListExpiredAsync(
        DateTimeOffset asOf, int batchSize, CancellationToken cancellationToken = default) =>
        await Posts
            .Where(post =>
                post.Status == PostStatus.Published &&
                post.ExpiresAt != null &&
                post.ExpiresAt <= asOf)
            .OrderBy(post => post.ExpiresAt)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

    public async Task<int> CountInAreaAsync(
        string hierarchyPathPrefix, CancellationToken cancellationToken = default) =>
        await Posts.CountAsync(
            post =>
                post.Status == PostStatus.Published &&
                post.Location.HierarchyPath != null &&
                post.Location.HierarchyPath.StartsWith(hierarchyPathPrefix),
            cancellationToken);

    public async Task<IReadOnlyDictionary<PostCategory, int>> CountCorroboratedAsync(
        string hierarchyPath,
        IReadOnlyCollection<PostCategory> categories,
        DateTimeOffset since,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(categories);

        if (string.IsNullOrWhiteSpace(hierarchyPath) || categories.Count == 0)
        {
            return new Dictionary<PostCategory, int>();
        }

        var path = hierarchyPath.TrimEnd('/');
        var beneath = path + "/";
        var wanted = categories.ToList();
        PostStatus[] happened = [PostStatus.Published, PostStatus.Expired, PostStatus.Cancelled];

        var rows = await Posts
            .AsNoTracking()
            .Where(post =>
                happened.Contains(post.Status)
                && post.VerificationStatus == PostVerificationStatus.CommunityVerified
                && post.Audience.Level == VisibilityLevel.Public
                && wanted.Contains(post.Category)
                && post.PublishedAt != null
                && post.PublishedAt >= since
                && post.Location.HierarchyPath != null
                && (post.Location.HierarchyPath == path || post.Location.HierarchyPath.StartsWith(beneath)))
            .GroupBy(post => post.Category)
            .Select(group => new { Category = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(row => row.Category, row => row.Count);
    }

    public async Task<int> CountInCountryAsync(
        string countryCode, CancellationToken cancellationToken = default) =>
        await Posts.CountAsync(
            post => post.Status == PostStatus.Published && post.Location.CountryCode == countryCode,
            cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, Guid>> GetAuthorsAsync(
        IReadOnlyCollection<Guid> postIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(postIds);

        if (postIds.Count == 0)
        {
            return new Dictionary<Guid, Guid>();
        }

        var ids = postIds.Select(CommunityPostId.From).ToList();

        var rows = await Posts
            .Where(post => ids.Contains(post.Id))
            .Select(post => new { post.Id, post.AuthorId })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(row => row.Id.Value, row => row.AuthorId.Value);
    }

    public async Task<(int PostCount, int AuthorCount)> CountPublishedByAuthorsAsync(
        IReadOnlyCollection<Guid> authorIds,
        DateTimeOffset from,
        DateTimeOffset until,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(authorIds);

        if (authorIds.Count == 0)
        {
            return (0, 0);
        }

        var authors = authorIds.Distinct().Select(UserId.From).ToList();

        // The authors, not the rows: the post count and the author count come from one read, and a
        // list of identifiers this size never leaves the server.
        var written = await Posts
            .AsNoTracking()
            .Where(post => authors.Contains(post.AuthorId)
                && post.PublishedAt != null
                && post.PublishedAt >= from
                && post.PublishedAt < until
                && post.Status != PostStatus.Draft
                && post.Status != PostStatus.Hidden
                && post.Status != PostStatus.Removed
                && post.Status != PostStatus.Deleted)
            .Select(post => post.AuthorId)
            .ToListAsync(cancellationToken);

        return (written.Count, written.Distinct().Count());
    }

    public async Task AddAsync(
        CommunityPost post, CancellationToken cancellationToken = default) =>
        await Posts.AddAsync(post, cancellationToken);

    public void Remove(CommunityPost post) => Posts.Remove(post);

    /// <summary>
    /// Narrows neighbourhood-only posts to the ones the reader is near (14 Oct 2026).
    /// <para>
    /// A pinned post is kept when its pin is inside the rectangle around the reader's circle; the
    /// feed then measures the circle exactly. An unpinned post is kept when its country is one of
    /// the reader's, or when it names no country at all. The reader's own posts are always kept.
    /// Every other audience passes through untouched.
    /// </para>
    /// </summary>
    private static IQueryable<CommunityPost> ApplyNeighborhoodAudience(
        IQueryable<CommunityPost> query, PostQueryFilter filter)
    {
        var viewer = filter.NeighborhoodViewer;
        var author = filter.NeighborhoodAuthorId;
        var countries = viewer?.Countries.Select(code => code.ToUpperInvariant()).ToList() ?? [];
        var box = viewer?.Box();

        if (viewer is null)
        {
            return author is null
                ? query.Where(post => post.Audience.Level != VisibilityLevel.Neighborhood)
                : query.Where(post =>
                    post.Audience.Level != VisibilityLevel.Neighborhood || post.AuthorId == author);
        }

        if (box is { } rectangle)
        {
            var (south, west, north, east) = rectangle;

            return author is null
                ? query.Where(post =>
                    post.Audience.Level != VisibilityLevel.Neighborhood
                    || (post.Location.Latitude != null
                        && post.Location.Longitude != null
                        && post.Location.Latitude >= south
                        && post.Location.Latitude <= north
                        && post.Location.Longitude >= west
                        && post.Location.Longitude <= east)
                    || (post.Location.Latitude == null
                        && (post.Location.CountryCode == null
                            || countries.Contains(post.Location.CountryCode))))
                : query.Where(post =>
                    post.Audience.Level != VisibilityLevel.Neighborhood
                    || post.AuthorId == author
                    || (post.Location.Latitude != null
                        && post.Location.Longitude != null
                        && post.Location.Latitude >= south
                        && post.Location.Latitude <= north
                        && post.Location.Longitude >= west
                        && post.Location.Longitude <= east)
                    || (post.Location.Latitude == null
                        && (post.Location.CountryCode == null
                            || countries.Contains(post.Location.CountryCode))));
        }

        // No active location: no pinned neighbourhood post can be near the reader.
        return author is null
            ? query.Where(post =>
                post.Audience.Level != VisibilityLevel.Neighborhood
                || (post.Location.Latitude == null
                    && (post.Location.CountryCode == null
                        || countries.Contains(post.Location.CountryCode))))
            : query.Where(post =>
                post.Audience.Level != VisibilityLevel.Neighborhood
                || post.AuthorId == author
                || (post.Location.Latitude == null
                    && (post.Location.CountryCode == null
                        || countries.Contains(post.Location.CountryCode))));
    }

    /// <summary>
    /// Translates the filter.
    /// <para>
    /// Comparisons are against the converted identifier types rather than their <c>Value</c>
    /// members — the translation trap B4 recorded, where <c>ids.Contains(x.Id.Value)</c> compiles
    /// and then fails at run time because EF cannot see through the conversion.
    /// </para>
    /// </summary>
    [SuppressMessage(
        "Globalization", "CA1304:Specify CultureInfo",
        Justification =
            "The ToLower calls are inside an expression tree. They are never executed in .NET: EF "
            + "translates them to SQL lower(), which runs under the database collation. There is no "
            + "CultureInfo to specify, and passing one would fail to translate.")]
    [SuppressMessage(
        "Globalization", "CA1311:Specify a culture or use an invariant version",
        Justification = "See CA1304. ToLowerInvariant translates identically; neither is executed.")]
    [SuppressMessage(
        "Globalization", "CA1862:Use the StringComparison method overloads",
        Justification =
            "The StringComparison overloads have no translation on either provider, so they would "
            + "move the search into memory. Lowering both sides is what makes the match "
            + "case-insensitive on Postgres, where = is not.")]
    private static IQueryable<CommunityPost> Apply(
        IQueryable<CommunityPost> query, PostQueryFilter filter)
    {
        query = query.Where(post => filter.Statuses.Contains(post.Status));

        if (filter.ExcludeExpired)
        {
            query = query.Where(post => post.Status != PostStatus.Expired);
        }

        if (filter.AllowedVisibility is { Count: > 0 } visibility)
        {
            query = query.Where(post => visibility.Contains(post.Audience.Level));

            if (visibility.Contains(VisibilityLevel.Neighborhood))
            {
                query = ApplyNeighborhoodAudience(query, filter);
            }
        }

        if (filter.AuthorIds is { Count: > 0 } authors)
        {
            query = query.Where(post => authors.Contains(post.AuthorId));
        }

        if (filter.OrganizationIds is { Count: > 0 } organizations)
        {
            query = query.Where(post =>
                post.Publisher.OrganizationId != null &&
                organizations.Contains(post.Publisher.OrganizationId.Value));
        }

        if (filter.PostIds is { Count: > 0 } postIds)
        {
            query = query.Where(post => postIds.Contains(post.Id));
        }

        if (filter.ExcludedAuthorIds is { Count: > 0 } blocked)
        {
            // Whole identifiers, like every other comparison in this method — see the summary
            // above. This one line said `.Value`, which is a member access on a type the store
            // does not have: it compiles, it reads correctly, and EF refuses to translate it.
            //
            // **Latent rather than live**, and worth being precise about: `ViewerContextResolver`
            // sets `BlockedMemberIds = []` unconditionally — "empty until Identity publishes a
            // block-list contract" — so this branch has never executed and no test could cover
            // it. It would have thrown on the first feed read after that contract landed, for
            // exactly the members who most need blocking to work. Found by B4.1's sweep after
            // the identical shape was fixed, live, in ServiceCategoryRepository.
            var excluded = blocked.Select(UserId.From).ToList();

            query = query.Where(post => !excluded.Contains(post.AuthorId));
        }

        if (filter.Category is { } category)
        {
            query = query.Where(post => post.Category == category);
        }

        if (filter.MinimumUrgency is { } urgency)
        {
            // Urgency is stored by name. As a text comparison "Urgent or above" matched Urgent
            // alone — Emergency sorts before it — so the most urgent posts were the ones left out.
            var admitted = StoredEnum.AtLeast(urgency);
            query = query.Where(post => admitted.Contains(post.Urgency));
        }

        if (!string.IsNullOrWhiteSpace(filter.CountryCode))
        {
            query = query.Where(post => post.Location.CountryCode == filter.CountryCode);
        }

        if (filter.AreaId is { } areaId)
        {
            query = query.Where(post => post.Location.AreaId == areaId);
        }

        if (!string.IsNullOrWhiteSpace(filter.HierarchyPathPrefix))
        {
            query = query.Where(post =>
                post.Location.HierarchyPath != null &&
                post.Location.HierarchyPath.StartsWith(filter.HierarchyPathPrefix));
        }

        if (!string.IsNullOrWhiteSpace(filter.CityName))
        {
            // Lowered on both sides (14 Oct 2026): Postgres compares text case-sensitively and the
            // SQLite tests do not.
            var city = filter.CityName.Trim().ToLowerInvariant();
            query = query.Where(post =>
                post.Location.CityName != null && post.Location.CityName.ToLower() == city);
        }

        if (!string.IsNullOrWhiteSpace(filter.HashtagSlug))
        {
            query = query.Where(post =>
                post.Hashtags.Any(hashtag => hashtag.Slug == filter.HashtagSlug));
        }

        if (filter.PublishedAfter is { } after)
        {
            query = query.Where(post => post.PublishedAt >= after);
        }

        if (filter.PublishedBefore is { } before)
        {
            query = query.Where(post => post.PublishedAt <= before);
        }

        if (filter.HasAffectedArea == true)
        {
            query = query.Where(post => post.AffectedRadiusMetres != null);
        }

        if (filter.BoundingBox is { } box)
        {
            // A post with no pin, in the town a circle search named (14 Oct 2026).
            var unpinned = string.IsNullOrWhiteSpace(filter.UnpinnedCityName)
                ? null
                : filter.UnpinnedCityName.Trim().ToLowerInvariant();
            var keepUnpinned = unpinned is not null;

            query = query.Where(post =>
                (post.Location.Latitude != null &&
                    post.Location.Longitude != null &&
                    post.Location.Latitude >= box.MinLatitude &&
                    post.Location.Latitude <= box.MaxLatitude &&
                    post.Location.Longitude >= box.MinLongitude &&
                    post.Location.Longitude <= box.MaxLongitude)
                || (keepUnpinned &&
                    post.Location.Latitude == null &&
                    post.Location.CityName != null &&
                    post.Location.CityName.ToLower() == unpinned));
        }

        if (filter.Cursor is { } cursor)
        {
            var (start, end) = FeedBoundary.Window(cursor);

            // Every identifier already returned in that millisecond. Excluding them is what
            // makes the boundary exact; *comparing* identifiers cannot be done here, because
            // these are value-converted strongly-typed ids and no provider translates an
            // ordering comparison on one. `post.Id.Value.CompareTo(...)` used to stand here and
            // threw "could not be translated" for every request carrying a cursor — page two of
            // every feed was a 500. See CursorBoundary.
            var tie = CursorBoundary.Exclusion(cursor.BoundaryIds, CommunityPostId.From);
            var seen = tie.Ids;
            var exhausted = tie.IsExhausted;

            // Rows strictly before the boundary millisecond, plus the remainder of the tie group
            // inside it.
            query = filter.PinnedFirst && cursor.LeadingFlag
                // The previous page ended inside the pinned block, so this one continues through
                // it and then falls into everything unpinned — all of which sorts after any
                // pinned row, however old.
                ? query.Where(post =>
                    !post.IsPinned
                    || post.PublishedAt < start
                    || (post.PublishedAt >= start
                        && post.PublishedAt < end
                        && !exhausted && !seen.Contains(post.Id)))
                : filter.PinnedFirst
                    // Past the pinned block. Re-admitting a pinned row here is what put an old
                    // pinned post back at the top of every page.
                    ? query.Where(post =>
                        !post.IsPinned
                        && (post.PublishedAt < start
                            || (post.PublishedAt >= start
                                && post.PublishedAt < end
                                && !exhausted && !seen.Contains(post.Id))))
                    : query.Where(post =>
                        post.PublishedAt < start
                        || (post.PublishedAt >= start
                            && post.PublishedAt < end
                            && !exhausted && !seen.Contains(post.Id)));
        }

        return query;
    }
}
