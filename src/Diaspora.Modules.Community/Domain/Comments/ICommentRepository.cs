using Diaspora.Modules.Community.Domain.Identifiers;
using Diaspora.Modules.Community.Domain.ValueObjects;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Pagination;

namespace Diaspora.Modules.Community.Domain.Comments;

/// <summary>Reads and writes comments, on any kind of content.</summary>
public interface ICommentRepository
{
    Task<Comment?> GetAsync(CommentId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Top-level comments on a piece of content, newest-or-pinned first, cursor-paged.
    /// <para>
    /// Top-level only, deliberately. A thread is fetched a page of roots at a time and replies
    /// are fetched per root, because a single flat page of a thousand comments returns the
    /// deepest corner of one argument and none of the other nine conversations.
    /// </para>
    /// </summary>
    Task<CursorPagedResult<Comment>> ListRootsAsync(
        ContentReference content,
        FeedCursor? cursor,
        int limit,
        bool includeHidden,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Every descendant of a comment, by prefix scan on the materialised path. The reason the
    /// path column exists.
    /// </summary>
    Task<IReadOnlyList<Comment>> ListSubtreeAsync(
        Comment root, int maxDepth, CancellationToken cancellationToken = default);

    /// <summary>Replies to many roots at once, so a thread page is two queries and not twenty.</summary>
    Task<IReadOnlyList<Comment>> ListRepliesAsync(
        IReadOnlyCollection<CommentId> rootIds,
        int maxPerRoot,
        CancellationToken cancellationToken = default);

    Task<int> CountAsync(
        ContentReference content, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<string, int>> CountManyAsync(
        IReadOnlyCollection<ContentReference> contents,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Comment>> ListByAuthorAsync(
        UserId authorId, int limit, CancellationToken cancellationToken = default);

    Task AddAsync(Comment comment, CancellationToken cancellationToken = default);

    void Remove(Comment comment);
}
