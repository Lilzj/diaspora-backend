using Diaspora.Modules.Community.Domain.Comments;
using Diaspora.Modules.Community.Domain.Identifiers;
using Diaspora.Modules.Community.Domain.ValueObjects;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Community.Integration;

/// <summary>
/// Comments, for a module that owns content but not conversation.
/// <para>
/// A module that has registered a content source can use this to add or read comments on its own
/// items in-process, without going through HTTP and without knowing what a comment row looks
/// like. Everything else about comments — threading, moderation, mentions, tombstoning — comes
/// for free and identically to every other surface.
/// </para>
/// </summary>
public interface ICommunityCommentService
{
    Task<Result<Comment>> AddAsync(
        ContentReference content,
        UserId authorId,
        string body,
        CommentId? parentCommentId = null,
        CancellationToken cancellationToken = default);

    Task<CommentThread> GetThreadAsync(
        ContentReference content,
        int rootLimit,
        bool includeHidden = false,
        CancellationToken cancellationToken = default);

    Task<int> CountAsync(ContentReference content, CancellationToken cancellationToken = default);
}
