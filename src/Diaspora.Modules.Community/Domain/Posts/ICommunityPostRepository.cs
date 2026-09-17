using Diaspora.Modules.Community.Domain.Enums;
using Diaspora.Modules.Community.Domain.Identifiers;
using Diaspora.Modules.Community.Domain.ValueObjects;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Pagination;

namespace Diaspora.Modules.Community.Domain.Posts;

/// <summary>
/// Reads and writes posts.
/// <para>
/// Two shapes of read, and the split is load-bearing. <see cref="GetAsync"/> materialises the
/// whole aggregate — attachments, hashtags, mentions, revisions, poll — because a command about
/// to mutate it needs all of it. The query methods materialise a <em>lighter</em> graph, because
/// a feed page of twenty cards that eagerly loaded fifty revisions each would be four hundred
/// extra rows nobody renders.
/// </para>
/// <para>
/// The feed query deliberately takes a <see cref="PostQueryFilter"/> rather than an
/// <c>IQueryable</c>. Handing an <c>IQueryable</c> to a feed provider would let a contributed
/// provider compose an untranslatable expression and fail at run time in another module's code —
/// which is exactly the failure mode B4 recorded when a value-converted member reached a
/// <c>StartsWith</c>.
/// </para>
/// </summary>
public interface ICommunityPostRepository
{
    /// <summary>The full aggregate. For commands.</summary>
    Task<CommunityPost?> GetAsync(
        CommunityPostId id, CancellationToken cancellationToken = default);

    /// <summary>The reading graph: attachments, hashtags, mentions and poll, but no revisions.</summary>
    Task<CommunityPost?> GetForReadAsync(
        CommunityPostId id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CommunityPost>> GetManyAsync(
        IReadOnlyCollection<CommunityPostId> ids, CancellationToken cancellationToken = default);

    /// <summary>The one query every feed provider runs, with a different filter each time.</summary>
    Task<CursorPagedResult<CommunityPost>> QueryAsync(
        PostQueryFilter filter, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CommunityPost>> ListDraftsAsync(
        UserId authorId, int limit, CancellationToken cancellationToken = default);

    /// <summary>Published posts past their expiry. The sweeper's query.</summary>
    Task<IReadOnlyList<CommunityPost>> ListExpiredAsync(
        DateTimeOffset asOf, int batchSize, CancellationToken cancellationToken = default);

    Task<int> CountInAreaAsync(
        string hierarchyPathPrefix, CancellationToken cancellationToken = default);

    /// <summary>
    /// Public posts in <paramref name="categories"/> that the community has corroborated, published
    /// since <paramref name="since"/> and filed at <paramref name="hierarchyPath"/> or beneath it,
    /// counted per category.
    /// <para>
    /// Withdrawn posts are not counted — their author said the report was wrong — and neither is
    /// anything moderated away. Expired and cancelled posts are: the thing they reported happened.
    /// </para>
    /// </summary>
    Task<IReadOnlyDictionary<PostCategory, int>> CountCorroboratedAsync(
        string hierarchyPath,
        IReadOnlyCollection<PostCategory> categories,
        DateTimeOffset since,
        CancellationToken cancellationToken = default);

    Task<int> CountInCountryAsync(
        string countryCode, CancellationToken cancellationToken = default);

    /// <summary>
    /// The authors of many posts, in one query.
    /// <para>
    /// Exists because almost every social write needs to know who wrote the thing being acted on
    /// — to notify them, to refuse a self-report, to skip a self-corroboration — and loading a
    /// whole post aggregate to read one identifier is the shape that makes commenting slow.
    /// </para>
    /// </summary>
    Task<IReadOnlyDictionary<Guid, Guid>> GetAuthorsAsync(
        IReadOnlyCollection<Guid> postIds, CancellationToken cancellationToken = default);

    Task AddAsync(CommunityPost post, CancellationToken cancellationToken = default);

    void Remove(CommunityPost post);
}
